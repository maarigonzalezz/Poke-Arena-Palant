using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PokeArena.Application.Decks;
using PokeArena.Application.Matchmaking;
using PokeArena.Infrastructure.Persistence;

namespace PokeArena.Tests.Integration;

// Recorre la API real de mazos y matchmaking sobre el mismo PostgreSQL desechable.
[Collection("catalog-integration")]
[Trait("Category", "Integration")]
public sealed class DeckMatchmakingTests(CatalogFixture fixture)
{
    private const string SearchRoute = "/api/player/matchmaking";
    private readonly Guid first = Guid.NewGuid();
    private readonly Guid second = Guid.NewGuid();
    private static readonly string[] CardCodes = Enumerable.Range(1, 12).Select(n => $"CONNECTED-{n:D2}").ToArray();

    private async Task Seed()
    {
        await fixture.Reset();
        await fixture.Execute($"""
            INSERT INTO players(id,identity_provider,provider_subject,cubes,elo)
            VALUES ('{first}','Google','connected-first',8,0),('{second}','Google','connected-second',8,50);
            INSERT INTO cards(code,name,cost,power,card_type,rarity,ability_code)
                SELECT 'CONNECTED-'||lpad(n::text,2,'0'),'Carta '||n,1,1,'Fuego','Común','HAB-000'
                FROM generate_series(1,12) n;
            INSERT INTO player_cards(player_id,card_id,copies)
                SELECT p.id,c.id,1 FROM players p CROSS JOIN cards c;
            """);
    }

    private HttpClient Client(Guid player) => fixture.Client("Player", player.ToString());

    private static async Task<DeckResult> CreateDeck(HttpClient client, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/player/decks") {
            Content = JsonContent.Create(new { name, cardCodes = CardCodes })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DeckResult>())!;
    }

    private static async Task<MatchSearchResult> Start(HttpClient client, string deckId)
    {
        using var response = await client.PostAsJsonAsync($"{SearchRoute}/ranked", new { deckId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MatchSearchResult>())!;
    }

    [IntegrationFact]
    public async Task Created_decks_are_selectable_and_the_worker_assigns_both_players_one_match()
    {
        await Seed();
        using var a = Client(first);
        using var b = Client(second);
        var firstDeck = await CreateDeck(a, "Primero");
        var secondDeck = await CreateDeck(b, "Segundo");
        var options = (await a.GetFromJsonAsync<MatchmakingDeckOption[]>($"{SearchRoute}/decks"))!;
        var option = Assert.Single(options);
        Assert.Equal(firstDeck.Id.ToString(), option.Id);
        Assert.Equal(firstDeck.Name, option.Name);
        var searches = await Task.WhenAll(Start(a, option.Id), Start(b, secondDeck.Id.ToString()));

        await using var instance = fixture.MatchmakingInstance();
        using var host = instance.CreateClient();
        var deadline = DateTime.UtcNow.AddSeconds(15);
        MatchSearchResult? current = null;
        while (DateTime.UtcNow < deadline)
        {
            current = await a.GetFromJsonAsync<MatchSearchResult>($"{SearchRoute}/current");
            if (current?.Status == "Matched") break;
            await Task.Delay(100);
        }
        Assert.NotNull(current);
        Assert.Equal("Matched", current.Status);
        Assert.NotNull(current.MatchId);
        var rival = (await b.GetFromJsonAsync<MatchSearchResult>($"{SearchRoute}/current"))!;
        Assert.Equal("Matched", rival.Status);
        Assert.Equal(current.MatchId, rival.MatchId);
        Assert.Equal(searches[0].Id, current.Id);
        Assert.Equal(1, await fixture.Count("SELECT count(*) FROM matches"));
        Assert.Equal(2, await fixture.Count("SELECT count(*) FROM match_players WHERE active"));
        Assert.Equal(2, await fixture.Count("""
            SELECT count(*) FROM match_players p
            WHERE cardinality(p.card_ids)=12 AND NOT EXISTS (
                SELECT 1 FROM player_deck_cards c WHERE c.deck_id=p.deck_id::uuid AND NOT (c.card_id=ANY(p.card_ids))
            )
            """));
        Assert.Equal(CardCodes, (await a.GetFromJsonAsync<DeckResult>($"/api/player/decks/{firstDeck.Id}"))!.CardCodes);
    }

    [IntegrationFact]
    public async Task Equivalent_uuid_formats_keep_one_search_and_cannot_bypass_the_deck_guard()
    {
        await Seed();
        using var client = Client(first);
        var deck = await CreateDeck(client, "Formatos UUID");
        // La API de mazos acepta Guid; matchmaking debe tratar sus formatos como el mismo identificador.
        var alternative = deck.Id.ToString("B").ToUpperInvariant();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/player/decks/{Uri.EscapeDataString(alternative)}")).StatusCode);
        var search = await Start(client, alternative);
        Assert.Equal(deck.Id.ToString(), search.DeckId);
        var retry = await Start(client, deck.Id.ToString("N"));
        Assert.Equal(search.Id, retry.Id);
        await using var scope = fixture.CreateScope();
        var guard = scope.ServiceProvider.GetRequiredService<IMatchmakingDeckGuard>();
        var db = scope.ServiceProvider.GetRequiredService<PokeArenaDbContext>();
        var failure = await Assert.ThrowsAsync<MatchmakingFailure>(() =>
            guard.WithUnlockedDeckAsync(first, alternative, ct => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE player_decks SET name={"Reservado"},name_key={"RESERVADO"} WHERE id={deck.Id}", ct), default));
        Assert.Equal("deck_locked", failure.Code);
        Assert.Equal(deck.Name, (await client.GetFromJsonAsync<DeckResult>($"/api/player/decks/{deck.Id}"))!.Name);
        Assert.Equal(1, await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE status='Searching'"));
    }

    [IntegrationFact]
    public async Task Switching_real_decks_requires_cancelling_and_keeps_both_saved_decks_unchanged()
    {
        await Seed();
        using var client = Client(first);
        var original = await CreateDeck(client, "Original");
        var replacement = await CreateDeck(client, "Alternativo");
        var search = await Start(client, original.Id.ToString());
        var rejected = await client.PostAsJsonAsync($"{SearchRoute}/ranked", new { deckId = replacement.Id });
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Contains("deck_locked", await rejected.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"{SearchRoute}/{search.Id}")).StatusCode);
        var next = await Start(client, replacement.Id.ToString());
        Assert.Equal(replacement.Id.ToString(), next.DeckId);
        Assert.NotEqual(search.Id, next.Id);
        Assert.Equal(2, (await client.GetFromJsonAsync<DeckResult[]>("/api/player/decks"))!.Length);
        Assert.Equal(24, await fixture.Count("SELECT count(*) FROM player_deck_cards"));
    }

    [IntegrationFact]
    public async Task Ownership_and_collection_are_rechecked_when_selecting_a_saved_deck()
    {
        await Seed();
        using var client = Client(first);
        using var other = Client(second);
        var deck = await CreateDeck(client, "Colección");
        var foreign = await other.PostAsJsonAsync($"{SearchRoute}/ranked", new { deckId = deck.Id });
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Contains("deck_not_found", await foreign.Content.ReadAsStringAsync());
        await fixture.Execute($"UPDATE player_cards SET copies=0 WHERE player_id='{first}' AND card_id=(SELECT min(id) FROM cards)");
        var unowned = await client.PostAsJsonAsync($"{SearchRoute}/ranked", new { deckId = deck.Id });
        Assert.Equal(HttpStatusCode.BadRequest, unowned.StatusCode);
        Assert.Contains("deck_cards_not_owned", await unowned.Content.ReadAsStringAsync());
        Assert.Equal(0, await fixture.Count("SELECT count(*) FROM matchmaking_searches"));
    }

    [IntegrationFact]
    public async Task A_bot_match_uses_the_deck_created_through_the_real_deck_api()
    {
        await Seed();
        using var client = Client(first);
        var deck = await CreateDeck(client, "Contra bot");
        var search = await Start(client, deck.Id.ToString());
        await fixture.Execute($"UPDATE matchmaking_searches SET created_at='{DateTime.UtcNow.AddSeconds(-61):O}'::timestamptz WHERE id='{search.Id}'");
        var response = await client.PostAsync($"{SearchRoute}/{search.Id}/bot", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var current = (await response.Content.ReadFromJsonAsync<MatchSearchResult>())!;
        Assert.Equal("Matched", current.Status);
        Assert.Equal(deck.Id.ToString(), current.DeckId);
        Assert.Equal(1, await fixture.Count($"SELECT count(*) FROM match_players WHERE match_id='{current.MatchId}' AND deck_id='{deck.Id}' AND cardinality(card_ids)=12"));
        Assert.Equal(CardCodes, (await client.GetFromJsonAsync<DeckResult>($"/api/player/decks/{deck.Id}"))!.CardCodes);
    }
}
