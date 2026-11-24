using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using PokeArena.Application.Matchmaking;
using PokeArena.Domain.Matchmaking;
using PokeArena.Infrastructure.Persistence;

namespace PokeArena.Tests.Integration;

// HTTP, cola, decks de prueba y concurrencia usan PostgreSQL real del contenedor desechable.
[Collection("catalog-integration")]
[Trait("Category","Integration")]
public sealed partial class MatchmakingTests(CatalogFixture fixture)
{
    private readonly Guid first = Guid.NewGuid();
    private readonly Guid second = Guid.NewGuid();

    private async Task Seed()
    {
        await fixture.Reset();
        await fixture.Execute($"""
            CREATE TABLE IF NOT EXISTS test_matchmaking_decks (
                id text PRIMARY KEY, player_id uuid NOT NULL REFERENCES players(id), name text NOT NULL);
            CREATE TABLE IF NOT EXISTS test_matchmaking_deck_cards (
                deck_id text REFERENCES test_matchmaking_decks(id), card_id bigint REFERENCES cards(id));
            DROP VIEW IF EXISTS matchmaking_deck_source;
            CREATE VIEW matchmaking_deck_source AS
                SELECT d.id AS deck_id,d.player_id,d.name,c.card_id
                FROM test_matchmaking_decks d LEFT JOIN test_matchmaking_deck_cards c ON c.deck_id=d.id;
            INSERT INTO players(id,identity_provider,provider_subject,cubes,elo)
            VALUES ('{first}','Google','first',8,0),('{second}','Google','second',8,50);
            INSERT INTO cards(code,name,cost,power,card_type,rarity,ability_code)
                SELECT 'MATCH-'||n,'Carta '||n,1,1,'Fuego','Común','HAB-000' FROM generate_series(1,13) n;
            UPDATE cards SET image_original_path='/files/card-original.png',image_small_path='/files/card-small.png'
                WHERE code LIKE 'MATCH-%';
            INSERT INTO locations(name,description,max_cards,energy_mode,extra_energy,effect_type,effect_parameters,image_status,active,current_version)
                VALUES
                    ('Arena uno','Prueba',4,'Fixed',1,'None','null','Ready',true,1),
                    ('Arena dos','Prueba',4,'Fixed',0,'None','null','Ready',true,1),
                    ('Arena tres','Prueba',4,'PerCard',1,'None','null','Ready',true,1);
            INSERT INTO player_cards(player_id,card_id,copies)
                SELECT p.id,c.id,1 FROM players p CROSS JOIN cards c WHERE c.code<>'MATCH-13';
            INSERT INTO test_matchmaking_decks VALUES
                ('first-deck','{first}','Primero'),('first-other','{first}','Otro'),('second-deck','{second}','Segundo');
            INSERT INTO test_matchmaking_deck_cards
                SELECT d.id,c.id FROM test_matchmaking_decks d CROSS JOIN cards c WHERE c.code<>'MATCH-13';
            """);
    }

    private HttpClient Client(Guid player) => fixture.Client("Player", player.ToString());
    private async Task<MatchSearchResult> Start(Guid player, string deck)
    {
        using var client = Client(player);
        var response = await client.PostAsJsonAsync("/api/player/matchmaking/ranked",new { deckId=deck });
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MatchSearchResult>())!;
    }

    private async Task<int> Process()
    {
        await using var scope = fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MatchmakingService>().ProcessAsync(default);
    }

    [IntegrationFact]
    public async Task Valid_search_checks_server_balance_and_creates_one_match_with_both_decks()
    {
        await Seed();
        var a = await Start(first,"first-deck");
        Assert.Equal("Searching",a.Status);
        await Start(second,"second-deck");
        Assert.Equal(1,await Process());
        Assert.Equal(0,await Process());
        Assert.Equal(1,await fixture.Count("SELECT count(*) FROM matches"));
        Assert.Equal(2,await fixture.Count("SELECT count(*) FROM match_players WHERE active"));
        Assert.Equal(2,await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE status='Matched'"));
        Assert.Equal(8,await fixture.Count("SELECT sum(cubes) FROM players"));
        Assert.Equal(4,await fixture.Count($"SELECT cubes FROM players WHERE id='{first}'"));
        Assert.Equal(4,await fixture.Count($"SELECT cubes FROM players WHERE id='{second}'"));
        using var client = Client(first);
        var current = (await client.GetFromJsonAsync<MatchSearchResult>("/api/player/matchmaking/current"))!;
        Assert.NotNull(current.MatchId);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/player/matchmaking/ranked",new { deckId="first-other" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.DeleteAsync($"/api/player/matchmaking/{a.Id}")).StatusCode);
        Assert.Equal(12,await fixture.Count("SELECT cardinality(card_ids) FROM match_players WHERE deck_id='first-deck'"));
    }

    [IntegrationFact]
    public async Task Match_state_shows_own_hand_and_public_opponent_counts_and_advances_after_timer()
    {
        await Seed();
        await Start(first, "first-deck");
        await Start(second, "second-deck");
        Assert.Equal(1, await Process());
        using var client = Client(first);
        var current = (await client.GetFromJsonAsync<MatchSearchResult>("/api/player/matchmaking/current"))!;
        var stateResponse = await client.GetAsync($"/api/player/matchmaking/{current.MatchId}/state");
        var initialJson = await stateResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("remainingCardIds", initialJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("preparationSeed", initialJson, StringComparison.OrdinalIgnoreCase);
        var initial = (await stateResponse.Content.ReadFromJsonAsync<MatchStateResult>())!;

        Assert.Equal(1, initial.TurnNumber);
        Assert.Equal(4, initial.WagerCubes);
        Assert.Equal(3, initial.Locations.Count);
        Assert.Equal(3, initial.Player.Energy);
        Assert.Equal(4, initial.Player.HandCards.Count);
        Assert.Equal(8, initial.Player.RemainingDeckCards);
        Assert.Equal(first, initial.Player.PlayerId);
        Assert.False(initial.Opponent.IsBot);
        Assert.Equal(4, initial.Opponent.HandCardCount);
        Assert.Equal(8, initial.Opponent.RemainingDeckCards);
        Assert.Empty(initial.Opponent.BoardCards);
        Assert.DoesNotContain("handCardIds", initialJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("botCardIds", initialJson, StringComparison.OrdinalIgnoreCase);

        await fixture.Execute($"UPDATE matches SET turn_ends_at=NOW()-INTERVAL '1 second' WHERE id='{current.MatchId}'");
        var advanceResponse = await client.PostAsync(
            $"/api/player/matchmaking/{current.MatchId}/turn/advance", null);
        Assert.True(advanceResponse.StatusCode == HttpStatusCode.OK,
            await advanceResponse.Content.ReadAsStringAsync());
        var next = (await advanceResponse.Content.ReadFromJsonAsync<MatchStateResult>())!;

        Assert.Equal(2, next.TurnNumber);
        Assert.Equal(5, next.Player.HandCards.Count);
        Assert.Equal(7, next.Player.RemainingDeckCards);
        Assert.Equal(4, next.Player.Energy);
        var retry = await client.PostAsJsonAsync($"/api/player/matchmaking/{current.MatchId}/turn/advance",
            new { turnNumber = 1 });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(2, (await retry.Content.ReadFromJsonAsync<MatchStateResult>())!.TurnNumber);
    }

    [IntegrationFact]
    public async Task Player_can_play_and_discard_cards_and_per_card_energy_applies_next_turn()
    {
        await Seed();
        await Start(first, "first-deck");
        await Start(second, "second-deck");
        Assert.Equal(1, await Process());
        using var client = Client(first);
        var current = (await client.GetFromJsonAsync<MatchSearchResult>("/api/player/matchmaking/current"))!;
        var initial = (await client.GetFromJsonAsync<MatchStateResult>(
            $"/api/player/matchmaking/{current.MatchId}/state"))!;
        var perCardLocation = initial.Locations.Single(location => location.EnergyMode == "PerCard");
        var playedCard = initial.Player.HandCards[0];
        var play = await client.PostAsJsonAsync($"/api/player/matchmaking/{current.MatchId}/cards/play",
            new { cardId = playedCard.Id, locationSlot = perCardLocation.Slot });
        Assert.Equal(HttpStatusCode.OK, play.StatusCode);
        var afterPlay = (await play.Content.ReadFromJsonAsync<MatchStateResult>())!;
        Assert.Equal(3, afterPlay.Player.HandCards.Count);
        Assert.Equal(2, afterPlay.Player.Energy);
        Assert.Contains(afterPlay.Player.BoardCards, card => card.Id == playedCard.Id
            && card.LocationSlot == perCardLocation.Slot);

        using var opponentClient = Client(second);
        var opponentInitial = (await opponentClient.GetFromJsonAsync<MatchStateResult>(
            $"/api/player/matchmaking/{current.MatchId}/state"))!;
        var opponentCard = opponentInitial.Player.HandCards[0];
        var opponentPlay = await opponentClient.PostAsJsonAsync(
            $"/api/player/matchmaking/{current.MatchId}/cards/play",
            new { cardId = opponentCard.Id, locationSlot = perCardLocation.Slot });
        Assert.Equal(HttpStatusCode.OK, opponentPlay.StatusCode);
        var opponentVisible = (await client.GetFromJsonAsync<MatchStateResult>(
            $"/api/player/matchmaking/{current.MatchId}/state"))!;
        Assert.Contains(opponentVisible.Opponent.BoardCards,
            card => card.Id == opponentCard.Id && card.LocationSlot == perCardLocation.Slot);
        Assert.Equal("/files/card-original.png",
            opponentVisible.Opponent.BoardCards.Single(card => card.Id == opponentCard.Id).ImagePath);

        var discardedCard = afterPlay.Player.HandCards[0];
        var discard = await client.PostAsJsonAsync($"/api/player/matchmaking/{current.MatchId}/cards/discard",
            new { cardId = discardedCard.Id });
        Assert.Equal(HttpStatusCode.OK, discard.StatusCode);
        var afterDiscard = (await discard.Content.ReadFromJsonAsync<MatchStateResult>())!;
        Assert.Contains(discardedCard.Id, afterDiscard.Player.CemeteryCardIds);
        Assert.Equal(2, afterDiscard.Player.HandCards.Count);
        Assert.Single(afterDiscard.Player.CemeteryCards);
        Assert.Equal("/files/card-original.png", afterDiscard.Player.CemeteryCards[0].ImagePath);

        await fixture.Execute($"UPDATE matches SET turn_ends_at=NOW()-INTERVAL '1 second' WHERE id='{current.MatchId}'");
        var advance = await client.PostAsync($"/api/player/matchmaking/{current.MatchId}/turn/advance", null);
        Assert.Equal(HttpStatusCode.OK, advance.StatusCode);
        var next = (await advance.Content.ReadFromJsonAsync<MatchStateResult>())!;
        Assert.Equal(3, next.Player.HandCards.Count);
        Assert.Equal(5, next.Player.Energy);
    }

    [IntegrationFact]
    public async Task Completing_match_records_winner_and_pays_ranked_pot_only_once()
    {
        await Seed();
        await Start(first, "first-deck");
        await Start(second, "second-deck");
        Assert.Equal(1, await Process());
        using var client = Client(first);
        var current = (await client.GetFromJsonAsync<MatchSearchResult>("/api/player/matchmaking/current"))!;
        await fixture.Execute($"""
            UPDATE match_players
            SET played_card_ids=ARRAY[(SELECT id FROM cards WHERE code='MATCH-1')],
                played_location_slots=ARRAY[1]
            WHERE match_id='{current.MatchId}' AND player_id='{first}';
            UPDATE matches SET turn_number=6,turn_ends_at=NOW()-INTERVAL '1 second'
            WHERE id='{current.MatchId}';
            """);

        var response = await client.PostAsJsonAsync($"/api/player/matchmaking/{current.MatchId}/turn/advance",
            new { turnNumber = 6 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<MatchStateResult>())!;
        Assert.Equal("Completed", result.Status);
        Assert.Equal("Win", result.Result!.Outcome);
        Assert.Equal(1, result.Result.PlayerLocationsWon);
        Assert.Equal(8, result.Result.CubesAwarded);
        Assert.Equal(12, await fixture.Count($"SELECT cubes FROM players WHERE id='{first}'"));
        Assert.Equal(4, await fixture.Count($"SELECT cubes FROM players WHERE id='{second}'"));

        var retry = await client.PostAsJsonAsync($"/api/player/matchmaking/{current.MatchId}/turn/advance",
            new { turnNumber = 6 });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal("Win", (await retry.Content.ReadFromJsonAsync<MatchStateResult>())!.Result!.Outcome);
        Assert.Equal(12, await fixture.Count($"SELECT cubes FROM players WHERE id='{first}'"));
    }

    [IntegrationFact]
    public async Task A_draw_returns_both_ranked_stakes()
    {
        await Seed();
        await Start(first, "first-deck");
        await Start(second, "second-deck");
        Assert.Equal(1, await Process());
        using var client = Client(first);
        var current = (await client.GetFromJsonAsync<MatchSearchResult>("/api/player/matchmaking/current"))!;
        await fixture.Execute($"UPDATE matches SET turn_number=6,turn_ends_at=NOW()-INTERVAL '1 second' WHERE id='{current.MatchId}'");

        var response = await client.PostAsJsonAsync($"/api/player/matchmaking/{current.MatchId}/turn/advance",
            new { turnNumber = 6 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<MatchStateResult>())!;
        Assert.Equal("Draw", result.Result!.Outcome);
        Assert.Equal(4, result.Result.CubesAwarded);
        Assert.Equal(8, await fixture.Count($"SELECT cubes FROM players WHERE id='{first}'"));
        Assert.Equal(8, await fixture.Count($"SELECT cubes FROM players WHERE id='{second}'"));
    }

    [IntegrationFact]
    public async Task Missing_playable_locations_keeps_both_searches_open_and_retries_when_fixed()
    {
        await Seed();
        await Start(first, "first-deck");
        await Start(second, "second-deck");
        await fixture.Execute("UPDATE locations SET active=false");

        Assert.Equal(0, await Process());
        Assert.Equal(0, await fixture.Count("SELECT count(*) FROM matches"));
        Assert.Equal(2, await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE status='Searching' AND end_reason IS NOT NULL"));

        await fixture.Execute("UPDATE locations SET active=true");
        Assert.Equal(1, await Process());
        Assert.Equal(1, await fixture.Count("SELECT count(*) FROM matches"));
    }

    [IntegrationFact]
    public async Task Tournament_matches_use_the_same_preparation_rules()
    {
        await Seed();
        var contextId = Guid.NewGuid();
        await using var scope = fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MatchmakingService>();
        await service.StartAsync(first, "first-deck", "Tournament", contextId, default);
        await service.StartAsync(second, "second-deck", "Tournament", contextId, default);

        Assert.Equal(1, await service.ProcessAsync(default));
        Assert.Equal(1, await fixture.Count("""
            SELECT count(*) FROM matches
            WHERE context='Tournament' AND context_id IS NOT NULL AND wager_cubes=0
                AND turn_number=1 AND turn_started_at IS NOT NULL AND turn_ends_at IS NOT NULL
            """));
        Assert.Equal(3, await fixture.Count("SELECT count(*) FROM match_location_choices"));
        Assert.Equal(8, await fixture.Count("SELECT sum(cardinality(hand_card_ids)) FROM match_players"));
    }

    [IntegrationFact]
    public async Task Player_can_save_an_owned_deck_and_enter_matchmaking_with_it()
    {
        await Seed();
        await fixture.Execute("""
            DROP VIEW matchmaking_deck_source;
            CREATE VIEW matchmaking_deck_source AS
            SELECT deck.id AS deck_id, deck.player_id, deck.name, cards.card_id
            FROM player_decks deck
            CROSS JOIN LATERAL unnest(deck.card_ids) AS cards(card_id);
            """);
        using var client = Client(first);
        var cardCodes = Enumerable.Range(1, 12).Select(n => $"MATCH-{n}").ToArray();
        var response = await client.PostAsJsonAsync("/api/player/decks",
            new { name = "Mazo de prueba", cardCodes });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var deck = (await response.Content.ReadFromJsonAsync<PlayerDeckResult>())!;
        Assert.Equal("Mazo de prueba", deck.Name);
        Assert.Equal(12, deck.CardCodes.Count);

        var options = await client.GetFromJsonAsync<IReadOnlyList<MatchmakingDeckOption>>(
            "/api/player/matchmaking/decks");
        Assert.Contains(options!, option => option.Id == deck.Id);
        var search = await Start(first, deck.Id);
        Assert.Equal("Searching", search.Status);
    }

    [IntegrationFact]
    public async Task Missing_foreign_invalid_and_unowned_cards_are_rejected_with_reasons()
    {
        await Seed();
        using var client = Client(first);
        foreach (var (deck,code,status) in new[] {
            ("","deck_required",HttpStatusCode.BadRequest),
            ("missing","deck_not_found",HttpStatusCode.BadRequest),
            ("second-deck","deck_not_owned",HttpStatusCode.Forbidden) })
        {
            var response = await client.PostAsJsonAsync("/api/player/matchmaking/ranked",new { deckId=deck });
            Assert.Equal(status,response.StatusCode);
            Assert.Contains(code,await response.Content.ReadAsStringAsync());
        }
        await fixture.Execute("DELETE FROM test_matchmaking_deck_cards WHERE deck_id='first-deck' AND card_id=(SELECT min(id) FROM cards)");
        Assert.Contains("invalid_deck",await (await client.PostAsJsonAsync("/api/player/matchmaking/ranked",new { deckId="first-deck" })).Content.ReadAsStringAsync());
        await fixture.Execute("""
            INSERT INTO test_matchmaking_deck_cards SELECT 'first-deck',max(id) FROM cards;
            """);
        Assert.Contains("deck_cards_not_owned",await (await client.PostAsJsonAsync("/api/player/matchmaking/ranked",new { deckId="first-deck" })).Content.ReadAsStringAsync());
        await fixture.Execute("UPDATE test_matchmaking_deck_cards SET card_id=(SELECT min(id) FROM cards) WHERE deck_id='first-deck'");
        Assert.Contains("invalid_deck",await (await client.PostAsJsonAsync("/api/player/matchmaking/ranked",new { deckId="first-deck" })).Content.ReadAsStringAsync());
        Assert.Equal(0,await fixture.Count("SELECT count(*) FROM matchmaking_searches"));
    }

    [IntegrationFact]
    public async Task Balance_is_read_from_database_and_authentication_is_required()
    {
        await Seed();
        using var anonymous = fixture.Client(null);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync("/api/player/matchmaking/ranked",new { deckId="first-deck" })).StatusCode);
        await fixture.Execute($"UPDATE players SET cubes=7 WHERE id='{first}'");
        using var client = Client(first);
        var response = await client.PostAsJsonAsync("/api/player/matchmaking/ranked",new { deckId="first-deck", cubes=999,elo=999,playerId=second });
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("insufficient_cubes",text);
        Assert.Contains("7",text);
        Assert.Contains("comprar",text);
        Assert.Equal(0,await fixture.Count("SELECT count(*) FROM matchmaking_searches"));
    }

    [IntegrationFact]
    public async Task Concurrent_requests_keep_one_search_and_prevent_deck_changes_until_cancelled()
    {
        await Seed();
        var results = await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Start(first,"first-deck")));
        Assert.Single(results.Select(x=>x.Id).Distinct());
        using var client = Client(first);
        var change = await client.PostAsJsonAsync("/api/player/matchmaking/ranked",new { deckId="first-other" });
        Assert.Equal(HttpStatusCode.Conflict,change.StatusCode);
        Assert.Contains("deck_locked",await change.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK,(await client.DeleteAsync($"/api/player/matchmaking/{results[0].Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.DeleteAsync($"/api/player/matchmaking/{results[0].Id}")).StatusCode);
        var another = await Start(first,"first-other");
        Assert.NotEqual(results[0].Id,another.Id);
        Assert.Equal(1,await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE status='Searching'"));
    }

    [IntegrationFact]
    public async Task Tournament_and_ranked_share_the_same_open_search_reservation()
    {
        await Seed();
        await using var scope = fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MatchmakingService>();
        var tournament = await service.StartAsync(first,"first-deck","Tournament",Guid.NewGuid(),default);
        using var client = Client(first);
        var response = await client.PostAsJsonAsync("/api/player/matchmaking/ranked",new { deckId="first-deck" });
        Assert.Equal(HttpStatusCode.Conflict,response.StatusCode);
        Assert.Contains("Tournament",await response.Content.ReadAsStringAsync());
        await service.CancelAsync(first,tournament.Id,default);
        await Start(first,"first-deck");
        var conflict = await Assert.ThrowsAsync<MatchmakingFailure>(()=>service.StartAsync(first,"first-deck","Tournament",Guid.NewGuid(),default));
        Assert.Equal("search_already_open",conflict.Code);
    }

    [IntegrationFact]
    public async Task Range_widens_up_to_the_cap_and_both_players_must_accept_it()
    {
        await Seed();
        await fixture.Execute($"UPDATE players SET elo=500 WHERE id='{second}'");
        await Start(first,"first-deck"); await Start(second,"second-deck");
        Assert.Equal(0,await Process());
        await fixture.Execute($"UPDATE matchmaking_searches SET created_at=NOW()-INTERVAL '3 minutes' WHERE player_id='{first}'");
        Assert.Equal(0,await Process());
        await fixture.Execute("UPDATE matchmaking_searches SET created_at=NOW()-INTERVAL '3 minutes'");
        Assert.Equal(1,await Process());
        await Seed();
        await fixture.Execute($"UPDATE players SET elo=501 WHERE id='{second}'");
        await Start(first,"first-deck"); await Start(second,"second-deck");
        await fixture.Execute("UPDATE matchmaking_searches SET created_at=NOW()-INTERVAL '1 hour'");
        Assert.Equal(0,await Process());
    }

    [IntegrationFact]
    public async Task Multiple_processors_never_create_two_matches_for_a_player()
    {
        await Seed();
        await Start(first,"first-deck"); await Start(second,"second-deck");
        var counts = await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Process()));
        Assert.Equal(1,counts.Sum());
        Assert.Equal(1,await fixture.Count("SELECT count(*) FROM matches"));
        Assert.Equal(2,await fixture.Count("SELECT count(*) FROM match_players"));
    }

    [IntegrationFact]
    public async Task Failure_between_matching_and_creation_rolls_back_and_both_remain_searching()
    {
        await Seed();
        await Start(first,"first-deck"); await Start(second,"second-deck");
        await fixture.Execute("""
            CREATE OR REPLACE FUNCTION reject_match_player() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'forced failure'; END $$;
            CREATE TRIGGER reject_match_player BEFORE INSERT ON match_players
                FOR EACH ROW EXECUTE FUNCTION reject_match_player();
            """);
        try { await Assert.ThrowsAnyAsync<Exception>(()=>Process()); }
        finally { await fixture.Execute("DROP TRIGGER reject_match_player ON match_players; DROP FUNCTION reject_match_player();"); }
        Assert.Equal(0,await fixture.Count("SELECT count(*) FROM matches"));
        Assert.Equal(0,await fixture.Count("SELECT count(*) FROM match_players"));
        Assert.Equal(2,await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE status='Searching'"));
        Assert.Equal(1,await Process());
    }

    [IntegrationFact]
    public async Task Deck_snapshot_cannot_be_changed_and_other_players_cannot_cancel_the_search()
    {
        await Seed();
        var search = await Start(first,"first-deck");
        using var other = Client(second);
        Assert.Equal(HttpStatusCode.NotFound,(await other.DeleteAsync($"/api/player/matchmaking/{search.Id}")).StatusCode);
        await fixture.Execute("DELETE FROM test_matchmaking_deck_cards WHERE deck_id='first-deck'");
        await Start(second,"second-deck");
        Assert.Equal(1,await Process());
        Assert.Equal(12,await fixture.Count("SELECT cardinality(card_ids) FROM match_players WHERE deck_id='first-deck'"));
    }

    [IntegrationFact]
    public async Task Missing_deck_integration_is_explicit_and_never_invents_decks()
    {
        await Seed();
        await fixture.Execute("DROP VIEW matchmaking_deck_source");
        using var client = Client(first);
        var list = await client.GetAsync("/api/player/matchmaking/decks");
        Assert.Equal(HttpStatusCode.ServiceUnavailable,list.StatusCode);
        Assert.Contains("decks_not_integrated",await list.Content.ReadAsStringAsync());
        var response = await client.PostAsJsonAsync("/api/player/matchmaking/ranked",new { deckId="first-deck" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        Assert.Contains("decks_not_integrated",await response.Content.ReadAsStringAsync());
        Assert.Equal(0,await fixture.Count("SELECT count(*) FROM matchmaking_searches"));
    }

    [IntegrationFact]
    public async Task Deck_selector_lists_only_owned_decks_once_in_name_order()
    {
        await Seed();
        using var client = Client(first);
        var response = await client.GetAsync("/api/player/matchmaking/decks");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var decks = (await response.Content.ReadFromJsonAsync<List<MatchmakingDeckOption>>())!;
        Assert.Equal(new[] { "first-other", "first-deck" },decks.Select(x=>x.Id));
        Assert.DoesNotContain(decks,x=>x.Id=="second-deck");
    }

    [IntegrationFact]
    public async Task Deck_edit_guard_blocks_changes_while_searching_and_while_playing()
    {
        await Seed();
        var search = await Start(first,"first-deck");
        await using var scope = fixture.CreateScope();
        var guard = scope.ServiceProvider.GetRequiredService<IMatchmakingDeckGuard>();
        var db = scope.ServiceProvider.GetRequiredService<PokeArenaDbContext>();
        Task<int> Edit(CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE test_matchmaking_decks SET name={'X'} WHERE id={"first-deck"}",ct);
        var failure = await Assert.ThrowsAsync<MatchmakingFailure>(()=>guard.WithUnlockedDeckAsync(first,"first-deck",Edit,default));
        Assert.Equal("deck_locked",failure.Code);
        using var client = Client(first);
        await client.DeleteAsync($"/api/player/matchmaking/{search.Id}");
        Assert.Equal(1,await guard.WithUnlockedDeckAsync(first,"first-deck",Edit,default));
        await Start(first,"first-deck"); await Start(second,"second-deck");
        await Process();
        Assert.Equal("deck_locked",(await Assert.ThrowsAsync<MatchmakingFailure>(()=>
            guard.WithUnlockedDeckAsync(first,"first-deck",Edit,default))).Code);
    }

    [IntegrationFact]
    public async Task A_player_who_spends_below_the_minimum_while_waiting_is_not_matched()
    {
        await Seed();
        await Start(first,"first-deck"); await Start(second,"second-deck");
        await fixture.Execute($"UPDATE players SET cubes=0 WHERE id='{first}'");
        Assert.Equal(0,await Process());
        using var client = Client(first);
        Assert.Equal("Cancelled",(await client.GetFromJsonAsync<MatchSearchResult>("/api/player/matchmaking/current"))!.Status);
        Assert.Equal(1,await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE status='Searching'"));
        Assert.Equal(0,await fixture.Count("SELECT count(*) FROM matches"));
    }

    [IntegrationFact]
    public async Task Two_running_apis_match_automatically_without_manual_processing()
    {
        await Seed();
        var search = await Start(first,"first-deck");
        await Start(second,"second-deck");
        await using var instanceA = fixture.MatchmakingInstance();
        await using var instanceB = fixture.MatchmakingInstance();
        using var hostA = instanceA.CreateClient();
        using var hostB = instanceB.CreateClient();
        await Eventually(async () => await fixture.Count("SELECT count(*) FROM matches") == 1);
        Assert.Equal(2,await fixture.Count("SELECT count(*) FROM match_players WHERE active"));
        using var firstClient = Client(first);
        using var secondClient = Client(second);
        var a = (await firstClient.GetFromJsonAsync<MatchSearchResult>("/api/player/matchmaking/current"))!;
        var b = (await secondClient.GetFromJsonAsync<MatchSearchResult>("/api/player/matchmaking/current"))!;
        Assert.Equal(search.Id,a.Id);
        Assert.Equal("Matched",a.Status);
        Assert.Equal(a.MatchId,b.MatchId);
        Assert.Equal(1,await fixture.Count("SELECT count(DISTINCT match_id) FROM match_players"));
    }

    [IntegrationFact]
    public async Task Automatic_worker_recovers_after_a_database_failure_without_blocking_players()
    {
        await Seed();
        await Start(first,"first-deck"); await Start(second,"second-deck");
        await fixture.Execute("""
            CREATE SEQUENCE test_worker_attempts;
            CREATE FUNCTION reject_worker_match() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                PERFORM nextval('test_worker_attempts');
                RAISE EXCEPTION 'temporary failure';
            END $$;
            CREATE TRIGGER reject_worker_match BEFORE INSERT ON match_players
                FOR EACH ROW EXECUTE FUNCTION reject_worker_match();
            """);
        await using var instance = fixture.MatchmakingInstance();
        using var host = instance.CreateClient();
        try
        {
            // La secuencia no se revierte: prueba que el worker intentó persistir y falló.
            await Eventually(async () => await fixture.Count("SELECT CASE WHEN is_called THEN last_value ELSE 0 END FROM test_worker_attempts") > 0);
            Assert.Equal(0,await fixture.Count("SELECT count(*) FROM matches"));
            Assert.Equal(2,await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE status='Searching'"));
        }
        finally
        {
            await fixture.Execute("DROP TRIGGER reject_worker_match ON match_players; DROP FUNCTION reject_worker_match(); DROP SEQUENCE test_worker_attempts;");
        }
        await Eventually(async () => await fixture.Count("SELECT count(*) FROM matches") == 1);
        Assert.Equal(2,await fixture.Count("SELECT count(*) FROM match_players"));
    }

    [IntegrationFact]
    public async Task A_search_survives_an_api_restart_and_matches_when_a_rival_arrives()
    {
        await Seed();
        await Start(first,"first-deck");
        using var client = Client(first);
        // Compara la fecha persistida, cuya precisión en PostgreSQL es de microsegundos.
        var original = (await client.GetFromJsonAsync<MatchSearchResult>("/api/player/matchmaking/current"))!;
        await using (var oldInstance = fixture.MatchmakingInstance())
        {
            using var host = oldInstance.CreateClient();
            await Eventually(async () => await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE last_checked_at>created_at") == 1);
        }
        await Start(second,"second-deck");
        await using var newInstance = fixture.MatchmakingInstance();
        using var newHost = newInstance.CreateClient();
        await Eventually(async () => await fixture.Count("SELECT count(*) FROM matches") == 1);
        var recovered = (await client.GetFromJsonAsync<MatchSearchResult>("/api/player/matchmaking/current"))!;
        Assert.Equal(original.Id,recovered.Id);
        Assert.Equal(original.CreatedAt,recovered.CreatedAt);
        Assert.Equal("Matched",recovered.Status);
    }

    [IntegrationFact]
    public async Task Cancelling_during_pairing_cannot_leave_a_partial_match()
    {
        await Seed();
        var search = await Start(first,"first-deck");
        await Start(second,"second-deck");
        using var client = Client(first);
        var cancel = client.DeleteAsync($"/api/player/matchmaking/{search.Id}");
        var process = Process();
        var response = await cancel;
        await process;
        var matches = await fixture.Count("SELECT count(*) FROM matches");
        if (response.StatusCode == HttpStatusCode.OK)
        {
            Assert.Equal(0,matches);
            Assert.Equal(0,await fixture.Count("SELECT count(*) FROM match_players"));
            Assert.Equal(1,await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE status='Searching'"));
        }
        else
        {
            Assert.Equal(HttpStatusCode.Conflict,response.StatusCode);
            Assert.Equal(1,matches);
            Assert.Equal(2,await fixture.Count("SELECT count(*) FROM match_players WHERE active"));
            Assert.Equal(2,await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE status='Matched'"));
        }
    }

    [IntegrationFact]
    public async Task Finished_matches_do_not_leave_players_or_decks_reserved()
    {
        await Seed();
        await Start(first,"first-deck"); await Start(second,"second-deck");
        await Process();
        // Simula únicamente el contrato persistente que ejecutará el motor al finalizar.
        await fixture.Execute($"BEGIN; UPDATE matches SET status='Completed'; UPDATE match_players SET active=FALSE; UPDATE players SET cubes=8 WHERE id='{first}'; COMMIT;");
        using var client = Client(first);
        var current = await client.GetFromJsonAsync<MatchSearchResult>("/api/player/matchmaking/current");
        Assert.Equal("Matched", current!.Status);
        Assert.Equal("Searching",(await Start(first,"first-other")).Status);
    }

    private static async Task Eventually(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(100);
        }
        Assert.True(await condition(),"El procesamiento automático no completó la operación dentro del tiempo esperado.");
    }
}
