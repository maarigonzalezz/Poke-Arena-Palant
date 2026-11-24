using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PokeArena.Application.Matchmaking;

namespace PokeArena.Tests.Integration;

// Envejece fechas persistidas para probar los plazos sin simular servicios ni esperar un minuto.
public sealed partial class MatchmakingTests
{
    private Task OfferBot(Guid player) => fixture.Execute(
        $"UPDATE matchmaking_searches SET created_at=NOW()-INTERVAL '61 seconds' WHERE player_id='{player}'");

    private Task StopPolling(Guid player) => fixture.Execute(
        $"UPDATE matchmaking_searches SET last_polled_at=NOW()-INTERVAL '61 seconds' WHERE player_id='{player}'");

    private static Task<HttpResponseMessage> AcceptBot(HttpClient client, Guid searchId) =>
        client.PostAsync($"/api/player/matchmaking/{searchId}/bot", null);

    [IntegrationFact]
    public async Task Bot_offer_is_computed_for_open_searches_and_remains_available_until_accepted()
    {
        await Seed();
        var search = await Start(first, "first-deck");
        Assert.False(search.BotAvailable);
        using var client = Client(first);
        var early = await AcceptBot(client, search.Id);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Contains("bot_not_available", await early.Content.ReadAsStringAsync());
        await OfferBot(first);
        for (var n = 0; n < 2; n++)
        {
            var current = (await client.GetFromJsonAsync<MatchSearchResult>("/api/player/matchmaking/current"))!;
            Assert.Equal("Searching", current.Status);
            Assert.True(current.BotAvailable);
            Assert.Null(current.MatchId);
        }
        // Modificar el origen después de entrar no cambia el deck reservado para la partida.
        await fixture.Execute("DELETE FROM test_matchmaking_deck_cards WHERE deck_id='first-deck'");
        var response = await AcceptBot(client, search.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var matched = (await response.Content.ReadFromJsonAsync<MatchSearchResult>())!;
        Assert.Equal("Matched", matched.Status);
        Assert.NotNull(matched.MatchId);
        Assert.False(matched.BotAvailable);
        Assert.Equal(1, await fixture.Count("SELECT count(*) FROM matches WHERE is_bot AND status='Active' AND cardinality(bot_card_ids)=12"));
        Assert.Equal(1, await fixture.Count("SELECT count(*) FROM match_players WHERE active AND deck_id='first-deck'"));
        Assert.Equal(1, await fixture.Count("""
            SELECT count(*) FROM matches m JOIN match_players p ON p.match_id=m.id
            WHERE ARRAY(SELECT unnest(m.bot_card_ids) ORDER BY 1)
                = ARRAY(SELECT unnest(p.card_ids) ORDER BY 1)
            """));
        Assert.Equal(12, await fixture.Count("SELECT sum(cubes) FROM players"));
        Assert.Equal(0, await Process());
        var cancel = await client.DeleteAsync($"/api/player/matchmaking/{search.Id}");
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Contains(matched.MatchId.ToString()!, await cancel.Content.ReadAsStringAsync());
    }

    [IntegrationFact]
    public async Task Concurrent_bot_acceptances_create_exactly_one_match_and_are_idempotent()
    {
        await Seed();
        var search = await Start(first, "first-deck");
        await OfferBot(first);
        using var client = Client(first);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => AcceptBot(client, search.Id)));
        var results = new List<MatchSearchResult>();
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            results.Add((await response.Content.ReadFromJsonAsync<MatchSearchResult>())!);
        }
        Assert.Single(results.Select(x => x.MatchId).Distinct());
        Assert.Equal(1, await fixture.Count("SELECT count(*) FROM matches WHERE is_bot"));
        Assert.Equal(1, await fixture.Count("SELECT count(*) FROM match_players WHERE active"));
        using var other = Client(second);
        Assert.Equal(HttpStatusCode.NotFound, (await AcceptBot(other, search.Id)).StatusCode);
    }

    [IntegrationFact]
    public async Task A_late_poll_expires_without_reviving_the_search_and_a_new_search_can_start()
    {
        await Seed();
        var search = await Start(first, "first-deck");
        await StopPolling(first);
        using var client = Client(first);
        var expired = (await client.GetFromJsonAsync<MatchSearchResult>("/api/player/matchmaking/current"))!;
        Assert.Equal("Expired", expired.Status);
        Assert.False(expired.BotAvailable);
        Assert.Null(expired.MatchId);
        Assert.Contains("60", expired.Message);
        var cancel = await client.DeleteAsync($"/api/player/matchmaking/{search.Id}");
        Assert.Equal("Expired", (await cancel.Content.ReadFromJsonAsync<MatchSearchResult>())!.Status);
        var replacement = await Start(first, "first-other");
        Assert.NotEqual(search.Id, replacement.Id);
        Assert.Equal("Searching", replacement.Status);
        Assert.Equal(1, await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE status='Expired'"));
        Assert.Equal(1, await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE status='Searching'"));
    }

    [IntegrationFact]
    public async Task Restarting_search_directly_releases_an_inactive_reservation_atomically()
    {
        await Seed();
        var search = await Start(first, "first-deck");
        await StopPolling(first);
        var replacement = await Start(first, "first-other");
        Assert.NotEqual(search.Id, replacement.Id);
        Assert.Equal(1, await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE status='Expired'"));
        Assert.Equal(1, await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE status='Searching'"));
    }

    [IntegrationFact]
    public async Task Polling_keeps_presence_and_does_not_reset_the_time_waiting_for_a_bot()
    {
        await Seed();
        await Start(first, "first-deck");
        await OfferBot(first);
        await fixture.Execute($"UPDATE matchmaking_searches SET last_polled_at=NOW()-INTERVAL '59 seconds' WHERE player_id='{first}'");
        using var client = Client(first);
        var current = (await client.GetFromJsonAsync<MatchSearchResult>("/api/player/matchmaking/current"))!;
        Assert.Equal("Searching", current.Status);
        Assert.True(current.BotAvailable);
        Assert.Equal(1, await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE last_polled_at>NOW()-INTERVAL '5 seconds' AND created_at<NOW()-INTERVAL '60 seconds'"));
    }

    [IntegrationFact]
    public async Task Inactive_candidates_are_never_paired_even_outside_the_expiration_batch()
    {
        await Seed();
        var search = await Start(first, "first-deck");
        await Start(second, "second-deck");
        await StopPolling(first);
        // Llena el lote anterior para que el candidato vencido se revise al intentar emparejarlo.
        await fixture.Execute("""
            INSERT INTO players(id,identity_provider,provider_subject,cubes,elo)
                SELECT gen_random_uuid(),'Google','inactive-'||n,8,0 FROM generate_series(1,100) n;
            INSERT INTO matchmaking_searches(id,player_id,deck_id,card_ids,context,elo,last_polled_at)
                SELECT gen_random_uuid(),id,'unused',ARRAY(SELECT id FROM cards ORDER BY id LIMIT 12),
                    'Ranked',0,NOW()-INTERVAL '5 minutes' FROM players WHERE provider_subject LIKE 'inactive-%';
            """);
        Assert.Equal(0, await Process());
        Assert.Equal(0, await fixture.Count("SELECT count(*) FROM matches"));
        using var client = Client(first);
        Assert.Equal("Expired", (await client.GetFromJsonAsync<MatchSearchResult>("/api/player/matchmaking/current"))!.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await AcceptBot(client, search.Id)).StatusCode);
    }

    [IntegrationFact]
    public async Task Accepting_a_bot_after_inactivity_commits_expiration_before_returning_conflict()
    {
        await Seed();
        var search = await Start(first, "first-deck");
        await OfferBot(first);
        await StopPolling(first);
        using var client = Client(first);
        var response = await AcceptBot(client, search.Id);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("search_expired", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE status='Expired'"));
        Assert.Equal(0, await fixture.Count("SELECT count(*) FROM matches"));
    }

    [IntegrationFact]
    public async Task Cancelled_searches_do_not_offer_a_bot_and_never_pair()
    {
        await Seed();
        var search = await Start(first, "first-deck");
        await OfferBot(first);
        using var client = Client(first);
        var response = await client.DeleteAsync($"/api/player/matchmaking/{search.Id}");
        Assert.Equal("Cancelled", (await response.Content.ReadFromJsonAsync<MatchSearchResult>())!.Status);
        await Start(second, "second-deck");
        Assert.Equal(0, await Process());
        Assert.Equal(HttpStatusCode.Conflict, (await AcceptBot(client, search.Id)).StatusCode);
        var current = (await client.GetFromJsonAsync<MatchSearchResult>("/api/player/matchmaking/current"))!;
        Assert.Equal("Cancelled", current.Status);
        Assert.False(current.BotAvailable);
        Assert.Equal(0, await fixture.Count("SELECT count(*) FROM matches"));
    }

    [IntegrationFact]
    public async Task Bot_acceptance_racing_with_human_pairing_reserves_the_player_only_once()
    {
        await Seed();
        var search = await Start(first, "first-deck");
        await OfferBot(first);
        await Start(second, "second-deck");
        using var client = Client(first);
        var accept = AcceptBot(client, search.Id);
        var processing = Process();
        var response = await accept;
        await processing;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<MatchSearchResult>())!;
        Assert.Equal("Matched", result.Status);
        Assert.Equal(1, await fixture.Count("SELECT count(*) FROM matches"));
        Assert.Equal(1, await fixture.Count($"SELECT count(*) FROM match_players WHERE player_id='{first}' AND active"));
        Assert.Equal(1, await fixture.Count($"SELECT count(*) FROM matches WHERE id='{result.MatchId}'"));
        var humans = await fixture.Count("SELECT count(*) FROM match_players");
        Assert.Equal(humans == 1 ? 1 : 0, await fixture.Count("SELECT count(*) FROM matches WHERE is_bot"));
    }

    [IntegrationFact]
    public async Task Cancelling_racing_with_bot_acceptance_has_a_single_consistent_outcome()
    {
        await Seed();
        var search = await Start(first, "first-deck");
        await OfferBot(first);
        using var client = Client(first);
        var cancel = client.DeleteAsync($"/api/player/matchmaking/{search.Id}");
        var accept = AcceptBot(client, search.Id);
        var cancelled = await cancel;
        var accepted = await accept;
        if (cancelled.StatusCode == HttpStatusCode.OK)
        {
            Assert.Equal(HttpStatusCode.Conflict, accepted.StatusCode);
            Assert.Equal(0, await fixture.Count("SELECT count(*) FROM matches"));
            Assert.Equal(1, await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE status='Cancelled'"));
        }
        else
        {
            Assert.Equal(HttpStatusCode.Conflict, cancelled.StatusCode);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            var result = (await accepted.Content.ReadFromJsonAsync<MatchSearchResult>())!;
            Assert.Contains(result.MatchId.ToString()!, await cancelled.Content.ReadAsStringAsync());
            Assert.Equal(1, await fixture.Count("SELECT count(*) FROM matches WHERE is_bot"));
            Assert.Equal(1, await fixture.Count("SELECT count(*) FROM match_players WHERE active"));
        }
    }

    [IntegrationFact]
    public async Task Bot_creation_failure_rolls_back_the_match_and_preserves_the_open_offer()
    {
        await Seed();
        var search = await Start(first, "first-deck");
        await OfferBot(first);
        await fixture.Execute("""
            CREATE FUNCTION reject_bot_participant() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'forced bot failure'; END $$;
            CREATE TRIGGER reject_bot_participant BEFORE INSERT ON match_players
                FOR EACH ROW EXECUTE FUNCTION reject_bot_participant();
            """);
        using var client = Client(first);
        try { Assert.Equal(HttpStatusCode.ServiceUnavailable, (await AcceptBot(client, search.Id)).StatusCode); }
        finally { await fixture.Execute("DROP TRIGGER reject_bot_participant ON match_players; DROP FUNCTION reject_bot_participant();"); }
        Assert.Equal(0, await fixture.Count("SELECT count(*) FROM matches"));
        Assert.Equal(0, await fixture.Count("SELECT count(*) FROM match_players"));
        var current = (await client.GetFromJsonAsync<MatchSearchResult>("/api/player/matchmaking/current"))!;
        Assert.Equal("Searching", current.Status);
        Assert.True(current.BotAvailable);
        Assert.Equal(HttpStatusCode.OK, (await AcceptBot(client, search.Id)).StatusCode);
    }

    [IntegrationFact]
    public async Task Bot_acceptance_rechecks_balance_and_never_accepts_a_tournament_search()
    {
        await Seed();
        var search = await Start(first, "first-deck");
        await OfferBot(first);
        await fixture.Execute($"UPDATE players SET cubes=7 WHERE id='{first}'");
        using var client = Client(first);
        Assert.Equal(HttpStatusCode.Conflict, (await AcceptBot(client, search.Id)).StatusCode);
        Assert.Equal(1, await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE status='Cancelled'"));
        await using var scope = fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MatchmakingService>();
        var tournament = await service.StartAsync(second, "second-deck", "Tournament", Guid.NewGuid(), default);
        await OfferBot(second);
        using var other = Client(second);
        Assert.Equal(HttpStatusCode.Conflict, (await AcceptBot(other, tournament.Id)).StatusCode);
        Assert.False((await other.GetFromJsonAsync<MatchSearchResult>("/api/player/matchmaking/current"))!.BotAvailable);
        Assert.Equal(0, await fixture.Count("SELECT count(*) FROM matches"));
    }

    [IntegrationFact]
    public async Task Running_worker_expires_an_unattended_search_without_a_client_request()
    {
        await Seed();
        await Start(first, "first-deck");
        await StopPolling(first);
        await using var instance = fixture.MatchmakingInstance();
        using var host = instance.CreateClient();
        await Eventually(async () => await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE status='Expired'") == 1);
        Assert.Equal(0, await fixture.Count("SELECT count(*) FROM matches"));
    }

    [IntegrationFact]
    public async Task Lifecycle_migration_can_be_reapplied_without_resetting_presence_or_matches()
    {
        await Seed();
        await Start(first, "first-deck");
        await OfferBot(first);
        await fixture.Execute($"UPDATE matchmaking_searches SET last_polled_at=NOW()-INTERVAL '30 seconds' WHERE player_id='{first}'");
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "TestData/matchmaking_lifecycle.sql"));
        await fixture.Execute(script);
        await fixture.Execute(script);
        Assert.Equal(1, await fixture.Count("SELECT count(*) FROM matchmaking_searches WHERE status='Searching' AND created_at<NOW()-INTERVAL '60 seconds' AND last_polled_at<NOW()-INTERVAL '20 seconds'"));
    }
}
