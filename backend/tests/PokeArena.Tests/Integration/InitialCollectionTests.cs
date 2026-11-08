using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PokeArena.Application.Authentication;
using PokeArena.Application.Inventory;

namespace PokeArena.Tests.Integration;

// Usa PostgreSQL y tokens propios reales; no sustituye Google por un proveedor simulado.
[Collection("authentication-integration")]
[Trait("Category", "Integration")]
public sealed class InitialCollectionTests(AuthenticationFixture fixture)
{
    private async Task<PlayerRegistration> Register(string subject)
    {
        await using var scope = fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetOrCreateAsync(
            new ExternalIdentity("Google", subject, null, null), CancellationToken.None);
    }

    private async Task<InitialCollectionResult> Deliver(Guid playerId)
    {
        await using var scope = fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<InventoryService>()
            .EnsureInitialCollectionAsync(playerId, CancellationToken.None);
    }

    private async Task<HttpClient> Client(PlayerRegistration registration)
    {
        await using var scope = fixture.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IPlatformTokenIssuer>().Issue(registration.Player);
        var client = fixture.AnonymousClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token.AccessToken);
        return client;
    }

    // Las imágenes Pending son intencionales para conservar el catálogo de pruebas del equipo.
    private Task Seed(int count, int start = 1) => fixture.Execute($"""
        INSERT INTO cards(code,name,cost,power,card_type,rarity,ability_code,image_status)
        SELECT 'TEST-'||n, 'Carta '||n, n%11, n%20, 'Fuego', 'Común', 'HAB-000', 'Pending'
        FROM generate_series({start},{start + count - 1}) n;
        """);

    [IntegrationFact]
    public async Task Delivery_has_fifteen_common_cards_a_deck_and_first_cube_movement()
    {
        await fixture.Reset(); await Seed(20);
        await fixture.Execute("""
            INSERT INTO cards(code,name,cost,power,card_type,rarity,ability_code,active)
            VALUES ('RARE','Rara',1,1,'Fuego','Rara','HAB-000',TRUE),
                   ('INACTIVE','Inactiva',1,1,'Fuego','Común','HAB-000',FALSE);
            """);
        var player = await Register("starter");
        Assert.True((await Deliver(player.Player.Id)).Completed);
        using var client = await Client(player);
        var result = await client.GetFromJsonAsync<PlayerProfileResult>("/api/player/inventory");
        Assert.Equal(1000, result!.Profile.Cubes);
        Assert.Equal(15, result.Collection.Count());
        Assert.Equal(15, result.Collection.Select(x => x.Id).Distinct().Count());
        Assert.All(result.Collection, card => { Assert.Equal("Común", card.Rarity); Assert.NotEqual("INACTIVE", card.Code); });
        Assert.Equal(12, result.SuggestedDeckCardIds.Distinct().Count());
        Assert.All(result.SuggestedDeckCardIds, id => Assert.Contains(result.Collection, card => card.Id == id));
        var history = await client.GetFromJsonAsync<CubeHistoryResult>("/api/player/inventory/cube-history");
        var first = Assert.Single(history!.Items);
        Assert.Equal(1, first.Sequence); Assert.Equal(1000, first.Amount); Assert.Equal(1000, first.BalanceAfter);
        Assert.Equal("InitialCollection", first.Reason); Assert.Null(history.NextSequence);
        Assert.True(result.InitialCollection.Completed);
    }

    [IntegrationFact]
    public async Task Insufficient_catalog_keeps_account_empty_and_retries_on_later_login()
    {
        await fixture.Reset(); await Seed(14);
        var player = await Register("pending");
        var pending = await Deliver(player.Player.Id);
        Assert.Equal("PendingCatalog", pending.Status); Assert.False(pending.Completed); Assert.NotNull(pending.Message);
        Assert.Equal(1, await fixture.Count("SELECT COUNT(*) FROM players"));
        Assert.Equal(0, await fixture.Count("SELECT COUNT(*) FROM player_cards"));
        Assert.Equal(0, await fixture.Count("SELECT COUNT(*) FROM player_cube_movements"));
        Assert.Equal(0, await fixture.Count("SELECT cubes FROM players"));
        await Seed(1, 15);
        var again = await Register("pending");
        Assert.False(again.IsNewPlayer);
        Assert.True((await Deliver(again.Player.Id)).Completed);
        Assert.Equal(15, await fixture.Count("SELECT COUNT(*) FROM player_cards"));
    }

    [IntegrationFact]
    public async Task Concurrent_first_logins_deliver_exactly_once_and_never_refill_spent_balance()
    {
        await fixture.Reset(); await Seed(60);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ => {
            await using var scope = fixture.CreateScope();
            var registration = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetOrCreateAsync(
                new ExternalIdentity("Google", "concurrent-delivery", null, null), CancellationToken.None);
            return await scope.ServiceProvider.GetRequiredService<InventoryService>()
                .EnsureInitialCollectionAsync(registration.Player.Id, CancellationToken.None);
        }));
        Assert.All(results, result => Assert.True(result.Completed));
        Assert.Equal(1, await fixture.Count("SELECT COUNT(*) FROM players"));
        Assert.Equal(15, await fixture.Count("SELECT COUNT(*) FROM player_cards"));
        Assert.Equal(1, await fixture.Count("SELECT COUNT(*) FROM player_cube_movements"));
        Assert.Equal(1000, await fixture.Count("SELECT cubes FROM players"));
        var player = await Register("concurrent-delivery");
        await fixture.Execute("UPDATE players SET cubes=0; DELETE FROM player_cards WHERE card_id=(SELECT MIN(card_id) FROM player_cards)");
        Assert.True((await Deliver(player.Player.Id)).Completed);
        Assert.Equal(0, await fixture.Count("SELECT cubes FROM players"));
        Assert.Equal(14, await fixture.Count("SELECT COUNT(*) FROM player_cards"));
        Assert.Equal(1, await fixture.Count("SELECT COUNT(*) FROM player_cube_movements"));
    }

    [IntegrationFact]
    public async Task Failures_in_cards_balance_or_history_roll_back_everything_and_allow_retry()
    {
        foreach (var table in new[] { "player_cards", "players", "player_cube_movements" })
        {
            await fixture.Reset(); await Seed(20);
            var player = await Register("failure-" + table);
            var action = table == "players" ? "UPDATE OF cubes" : "INSERT";
            await fixture.Execute($"""
                CREATE OR REPLACE FUNCTION fail_initial_delivery() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'fallo de entrega para prueba'; END; $$;
                CREATE TRIGGER fail_delivery BEFORE {action} ON {table}
                    FOR EACH ROW EXECUTE FUNCTION fail_initial_delivery();
                """);
            try
            {
                Assert.Equal("PendingRetry", (await Deliver(player.Player.Id)).Status);
                Assert.Equal(1, await fixture.Count("SELECT COUNT(*) FROM players"));
                Assert.Equal(0, await fixture.Count("SELECT cubes FROM players"));
                Assert.Equal(0, await fixture.Count("SELECT COUNT(*) FROM player_cards"));
                Assert.Equal(0, await fixture.Count("SELECT COUNT(*) FROM player_cube_movements"));
            }
            finally { await fixture.Execute($"DROP TRIGGER fail_delivery ON {table}; DROP FUNCTION fail_initial_delivery();"); }
            Assert.True((await Deliver(player.Player.Id)).Completed);
            Assert.Equal(15, await fixture.Count("SELECT COUNT(*) FROM player_cards"));
            Assert.Equal(1000, await fixture.Count("SELECT cubes FROM players"));
            Assert.Equal(1, await fixture.Count("SELECT COUNT(*) FROM player_cube_movements"));
        }
    }

    [IntegrationFact]
    public async Task Inventory_is_private_read_only_and_ignores_client_profile_and_new_player_flag()
    {
        await fixture.Reset(); await Seed(20);
        var owner = await Register("owner"); var victim = await Register("victim");
        await Deliver(victim.Player.Id);
        using var anonymous = fixture.AnonymousClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/player/inventory")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/player/inventory/cube-history")).StatusCode);
        using var client = await Client(owner);
        var result = await client.GetFromJsonAsync<PlayerProfileResult>(
            $"/api/player/inventory?Profile.Id={victim.Player.Id}&NewPlayer=true&Profile.Cubes=99999");
        Assert.Equal(owner.Player.Id, result!.Profile.Id); Assert.Equal(0, result.Profile.Cubes);
        Assert.Empty(result.Collection); Assert.Empty(result.SuggestedDeckCardIds);
        Assert.Empty((await client.GetFromJsonAsync<CubeHistoryResult>(
            $"/api/player/inventory/cube-history?Profile.Id={victim.Player.Id}"))!.Items);
        Assert.Equal(15, await fixture.Count("SELECT COUNT(*) FROM player_cards"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/player/inventory/cube-history?after=-1")).StatusCode);
        await fixture.Execute($"UPDATE players SET active=FALSE WHERE id='{owner.Player.Id}'");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/player/inventory")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/player/inventory/cube-history")).StatusCode);
    }

    [IntegrationFact]
    public async Task History_pages_are_stable_and_initial_bonus_cannot_be_inserted_twice()
    {
        await fixture.Reset(); await Seed(15);
        var player = await Register("history");
        await Deliver(player.Player.Id);
        await fixture.Execute($"""
            INSERT INTO player_cube_movements(player_id,sequence,amount,balance_after,reason)
            SELECT '{player.Player.Id}'::uuid,n,1,1000+n-1,'TestReward' FROM generate_series(2,60) n;
            UPDATE players SET cubes=1059 WHERE id='{player.Player.Id}';
            """);
        using var client = await Client(player);
        var first = await client.GetFromJsonAsync<CubeHistoryResult>("/api/player/inventory/cube-history");
        Assert.Equal(50, first!.Items.Count); Assert.Equal(50, first.NextSequence);
        Assert.Equal("InitialCollection", first.Items[0].Reason);
        var last = await client.GetFromJsonAsync<CubeHistoryResult>("/api/player/inventory/cube-history?after=50");
        Assert.Equal(10, last!.Items.Count); Assert.Null(last.NextSequence);
        Assert.Equal(60, first.Items.Concat(last.Items).Select(x => x.Sequence).Distinct().Count());
        var conflict = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => fixture.Execute($"""
            INSERT INTO player_cube_movements(player_id,sequence,amount,balance_after,reason)
            VALUES ('{player.Player.Id}',1,1000,1000,'InitialCollection');
            """));
        Assert.Equal("23505", conflict.SqlState);
        Assert.True((await Deliver(player.Player.Id)).Completed);
        Assert.Equal(1059, await fixture.Count("SELECT cubes FROM players"));
    }

    [IntegrationFact]
    public async Task Migration_is_repeatable_and_preserves_legacy_inventory_without_inventing_a_bonus()
    {
        await fixture.Reset(); await Seed(20);
        var player = await Register("legacy");
        await fixture.Execute($"UPDATE players SET cubes=300 WHERE id='{player.Player.Id}'");
        var migration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "TestData/initial_collection.sql"));
        await fixture.Execute(migration); await fixture.Execute(migration);
        Assert.Equal("LegacyReview", (await Deliver(player.Player.Id)).Status);
        Assert.Equal(300, await fixture.Count("SELECT cubes FROM players"));
        Assert.Equal(0, await fixture.Count("SELECT COUNT(*) FROM player_cube_movements"));
    }
}
