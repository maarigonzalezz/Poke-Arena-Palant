using System.Net;
using System.Net.Http.Json;
using PokeArena.Application.Catalog;

namespace PokeArena.Tests.Integration;

/// <summary>Verifica la consulta real y sus joins contra PostgreSQL desechable.</summary>
[Collection("catalog-integration")]
[Trait("Category", "Integration")]
public sealed class PlayerCatalogApiTests(CatalogFixture fixture)
{
    private readonly Guid playerId = Guid.NewGuid();
    private readonly Guid otherPlayerId = Guid.NewGuid();

    private async Task Seed()
    {
        await fixture.Reset();
        await fixture.Execute($"""
            INSERT INTO players(id,identity_provider,provider_subject)
            VALUES ('{playerId}','Google','catalog-player'), ('{otherPlayerId}','Google','other-player');
            INSERT INTO cards(code,name,cost,power,card_type,rarity,ability_code,image_status,image_original_path,image_small_path,active)
            VALUES ('F1','Fuego propio',3,4,'Fuego','Común','HAB-000','Ready','/files/large1.png','/files/small1.png',TRUE),
                   ('F2','Fuego ajeno',3,5,'Fuego','Común','HAB-000','Ready','/files/large2.png','/files/small2.png',TRUE),
                   ('A1','Agua propia',2,6,'Agua','Rara','HAB-000','Ready','/files/large3.png','/files/small3.png',TRUE),
                   ('P1','Pendiente',3,4,'Fuego','Común','HAB-000','Pending',NULL,NULL,TRUE),
                   ('I1','Inactiva',3,4,'Fuego','Común','HAB-000','Ready','/files/large4.png','/files/small4.png',FALSE);
            INSERT INTO player_cards(player_id,card_id,copies)
            SELECT '{playerId}',id,3 FROM cards WHERE code='F1';
            INSERT INTO player_cards(player_id,card_id,copies)
            SELECT '{playerId}',id,1 FROM cards WHERE code='A1';
            INSERT INTO player_cards(player_id,card_id,copies)
            SELECT '{otherPlayerId}',id,7 FROM cards WHERE code='F2';
            """);
    }

    [IntegrationFact]
    public async Task Filters_ownership_and_large_images_are_combined_in_database()
    {
        await Seed();
        using var client = fixture.Client("Player", playerId.ToString());
        var page = await client.GetFromJsonAsync<PlayerCatalogPage>("/api/player/catalog?type=Fuego&cost=3");
        Assert.Equal(2, page!.TotalCount);
        var owned = Assert.Single(page.Items, item => item.Owned);
        Assert.Equal("F1", owned.Code);
        Assert.Equal(3, owned.Quantity);
        Assert.Equal("/files/large1.png", owned.ImageUrl);
        Assert.Equal(0, Assert.Single(page.Items, item => !item.Owned).Quantity);

        var collection = await client.GetFromJsonAsync<PlayerCatalogPage>("/api/player/collection?type=Fuego&cost=3&ownedOnly=false");
        Assert.Equal("F1", Assert.Single(collection!.Items).Code);
        var onlyOwned = await client.GetFromJsonAsync<PlayerCatalogPage>("/api/player/catalog?ownedOnly=true");
        Assert.Equal(2, onlyOwned!.TotalCount);
        Assert.All(onlyOwned.Items, item => Assert.True(item.Owned));

        using var other = fixture.Client("Player", otherPlayerId.ToString());
        var otherCollection = await other.GetFromJsonAsync<PlayerCatalogPage>("/api/player/collection");
        Assert.Equal(7, Assert.Single(otherCollection!.Items).Quantity);
    }

    [IntegrationFact]
    public async Task Cursor_pages_do_not_repeat_cards_and_empty_results_are_successful()
    {
        await Seed();
        using var client = fixture.Client("Player", playerId.ToString());
        var first = await client.GetFromJsonAsync<PlayerCatalogPage>("/api/player/catalog?pageSize=2");
        Assert.Equal(3, first!.TotalCount);
        Assert.Equal(2, first.Items.Count);
        Assert.NotNull(first.NextPosition);
        var second = await client.GetFromJsonAsync<PlayerCatalogPage>($"/api/player/catalog?pageSize=2&after={first.NextPosition}");
        Assert.Single(second!.Items);
        Assert.Null(second.NextPosition);
        Assert.DoesNotContain(second.Items[0].Id, first.Items.Select(item => item.Id));
        var empty = await client.GetFromJsonAsync<PlayerCatalogPage>("/api/player/catalog?type=Agua&cost=10");
        Assert.Empty(empty!.Items);
        Assert.Equal(0, empty.TotalCount);
        Assert.Null(empty.NextPosition);
    }

    [IntegrationFact]
    public async Task Fresh_queries_show_new_cards_and_updated_copy_counts()
    {
        await Seed();
        using var client = fixture.Client("Player", playerId.ToString());
        await fixture.Execute($"""
            UPDATE player_cards SET copies=5
            WHERE player_id='{playerId}' AND card_id=(SELECT id FROM cards WHERE code='F1');
            INSERT INTO cards(code,name,cost,power,card_type,rarity,ability_code,image_status,image_original_path)
            VALUES ('NEW','Nueva',1,1,'Fuego','Común','HAB-000','Ready','/files/new-large.png');
            """);
        var page = await client.GetFromJsonAsync<PlayerCatalogPage>("/api/player/catalog");
        Assert.Equal(4, page!.TotalCount);
        Assert.Contains(page.Items, item => item.Code == "NEW" && !item.Owned);
        Assert.Equal(5, Assert.Single(page.Items, item => item.Code == "F1").Quantity);
    }

    [IntegrationFact]
    public async Task Invalid_inputs_return_400_and_anonymous_requests_return_401()
    {
        await Seed();
        using var client = fixture.Client("Player", playerId.ToString());
        foreach (var query in new[] { "type=Otro", "cost=11", "cost=abc", "after=-1", "after=abc", "pageSize=0", "pageSize=101", "ownedOnly=abc" })
        {
            var response = await client.GetAsync("/api/player/catalog?" + query);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("errors", await response.Content.ReadAsStringAsync());
        }
        using var anonymous = fixture.Client(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/player/catalog")).StatusCode);
        await fixture.Execute($"UPDATE players SET active=FALSE WHERE id='{playerId}'");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/player/catalog")).StatusCode);
    }
}
