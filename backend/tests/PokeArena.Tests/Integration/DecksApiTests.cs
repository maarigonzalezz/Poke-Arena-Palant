using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PokeArena.Application.Decks;
using PokeArena.Application.Matchmaking;

namespace PokeArena.Tests.Integration;

[Collection("catalog-integration")]
[Trait("Category", "Integration")]
public sealed class DecksApiTests(CatalogFixture fixture)
{
    private const string Route = "/api/player/decks";
    private static readonly string[] Cards = Enumerable.Range(1, 12).Select(i => $"DECK-{i:D2}").ToArray();
    private readonly Guid ownerId = Guid.NewGuid();
    private readonly Guid otherId = Guid.NewGuid();

    [IntegrationFact]
    public async Task Saves_multiple_decks_and_exposes_only_the_owners_decks()
    {
        await Seed();
        using var owner = fixture.Client("Player", ownerId.ToString());
        using var other = fixture.Client("Player", otherId.ToString());
        using var anonymous = fixture.AnonymousClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Route)).StatusCode);

        var firstResponse = await Create(owner, "  Fuego  ", Cards, "same-key");
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var first = (await firstResponse.Content.ReadFromJsonAsync<DeckResult>())!;
        Assert.Equal("Fuego", first.Name);
        Assert.Equal(Cards, first.CardCodes);
        Assert.Equal(HttpStatusCode.Created, (await Create(owner, "Agua", Cards, "second-key")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Create(other, "Fuego", Cards, "same-key")).StatusCode);

        var ownDeck = (await owner.GetFromJsonAsync<DeckResult>(firstResponse.Headers.Location))!;
        Assert.Equal(first.Id, ownDeck.Id);
        Assert.Equal(first.CardCodes, ownDeck.CardCodes);
        Assert.Equal(first.CreatedAt, ownDeck.CreatedAt);
        Assert.Equal(Cards, ownDeck.Cards!.Select(card => card.Code));
        Assert.Equal("Carta 1", ownDeck.Cards![0].Name);
        Assert.Equal("Fuego", ownDeck.Cards[0].Type);
        Assert.Equal(2, (await owner.GetFromJsonAsync<DeckResult[]>(Route))!.Length);
        Assert.DoesNotContain((await other.GetFromJsonAsync<DeckResult[]>(Route))!, deck => deck.Id == first.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(firstResponse.Headers.Location)).StatusCode);

        var options = (await owner.GetFromJsonAsync<MatchmakingDeckOption[]>("/api/player/matchmaking/decks"))!;
        Assert.Contains(options, deck => deck.Id == first.Id.ToString());
        var foreign = await other.PostAsJsonAsync("/api/player/matchmaking/ranked", new { deckId = first.Id });
        await Error(foreign, HttpStatusCode.NotFound, "deck_not_found");
        var ranked = await owner.PostAsJsonAsync("/api/player/matchmaking/ranked", new { deckId = first.Id });
        Assert.Equal(HttpStatusCode.OK, ranked.StatusCode);
    }

    [IntegrationFact]
    public async Task Rejects_invalid_counts_duplicates_and_unowned_cards_without_saving()
    {
        await Seed();
        using var owner = fixture.Client("Player", ownerId.ToString());
        foreach (var codes in new[] { Cards[..^1], Cards.Append("DECK-13").ToArray() })
        {
            var details = await Error(await Create(owner, "Fuego", codes, "validation"),
                HttpStatusCode.BadRequest, "invalid_deck_size");
            Assert.Equal(codes.Length, details.GetProperty("actualCount").GetInt32());
            Assert.Equal(12, details.GetProperty("expectedCount").GetInt32());
        }

        var duplicates = await Error(await Create(owner, "Fuego", [.. Cards[..^2], Cards[0], Cards[1]], "validation"),
            HttpStatusCode.BadRequest, "duplicate_deck_cards");
        Assert.Equal(Cards[..2], duplicates.GetProperty("cardCodes").Deserialize<string[]>());

        var unowned = await Error(await Create(owner, "Fuego", [.. Cards[..^2], "DECK-13", "NO-EXISTE"], "validation"),
            HttpStatusCode.BadRequest, "deck_cards_not_owned");
        Assert.Equal(new[] { "DECK-13", "NO-EXISTE" }, unowned.GetProperty("cardCodes").Deserialize<string[]>());
        Assert.Equal(0, await fixture.Count("SELECT count(*) FROM player_decks"));
        Assert.Equal(0, await fixture.Count("SELECT count(*) FROM player_deck_cards"));
        Assert.Equal(0, await fixture.Count("SELECT count(*) FROM idempotency_requests"));
    }

    [IntegrationFact]
    public async Task Concurrent_retries_replay_and_independent_requests_conflict_on_the_name()
    {
        await Seed();
        using var owner = fixture.Client("Player", ownerId.ToString());
        var original = await Task.WhenAll(
            Create(owner, "Dragón", Cards, "retry"),
            Create(owner, "Dragón", Cards.Reverse().ToArray(), "retry"));
        Assert.All(original, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.Equal(original[0].Headers.Location, original[1].Headers.Location);
        await Error(await Create(owner, "Agua", Cards, "retry"),
            HttpStatusCode.Conflict, "idempotency_key_conflict");
        await Error(await Create(owner, "  DRAGÓN  ", Cards, "new-key"),
            HttpStatusCode.Conflict, "deck_name_conflict");

        var competing = await Task.WhenAll(
            Create(owner, "Agua", Cards, "parallel-a"),
            Create(owner, "agua", Cards, "parallel-b"));
        Assert.Single(competing, response => response.StatusCode == HttpStatusCode.Created);
        await Error(Assert.Single(competing, response => response.StatusCode == HttpStatusCode.Conflict),
            HttpStatusCode.Conflict, "deck_name_conflict");
        Assert.Equal(2, await fixture.Count("SELECT count(*) FROM player_decks"));
        Assert.Equal(24, await fixture.Count("SELECT count(*) FROM player_deck_cards"));
    }

    private async Task Seed()
    {
        await fixture.Reset();
        await fixture.Execute($"""
            INSERT INTO players(id, identity_provider, provider_subject, cubes)
            VALUES ('{ownerId}', 'Google', 'deck-owner', 8), ('{otherId}', 'Google', 'deck-other', 8);
            INSERT INTO cards(code, name, cost, power, card_type, rarity, ability_code)
            SELECT 'DECK-' || lpad(n::text, 2, '0'), 'Carta ' || n, 1, 1, 'Fuego', 'Común', 'HAB-000'
            FROM generate_series(1, 13) n;
            INSERT INTO player_cards(player_id, card_id, copies)
            SELECT p.id, c.id, CASE WHEN c.code = 'DECK-13' THEN 0 ELSE 1 END
            FROM players p CROSS JOIN cards c;
            """);
    }

    private static async Task<HttpResponseMessage> Create(HttpClient client, string name, string[] cardCodes, string key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Route) { Content = JsonContent.Create(new { name, cardCodes }) };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> Error(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, error.GetProperty("type").GetString());
        return error.GetProperty("details");
    }
}
