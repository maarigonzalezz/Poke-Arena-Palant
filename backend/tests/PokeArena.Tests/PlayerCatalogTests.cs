using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using PokeArena.Api.Controllers;
using PokeArena.Application.Catalog;
using PokeArena.Application.Common;
using PokeArena.Domain.Catalogs;

namespace PokeArena.Tests;

public sealed class PlayerCatalogTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public async Task Invalid_cost_explains_allowed_range_without_querying_database(int cost)
    {
        var repository = new RecordingRepository();
        var service = new PlayerCatalogService(repository);
        var error = await Assert.ThrowsAsync<CatalogValidationException>(() =>
            service.BrowseAsync(Guid.NewGuid(), new() { Cost = cost }, CancellationToken.None));
        Assert.Contains("0 y 10", error.Errors["cost"]);
        Assert.Null(repository.Query);
    }

    [Fact]
    public async Task Invalid_type_lists_all_allowed_types()
    {
        var service = new PlayerCatalogService(new RecordingRepository());
        var error = await Assert.ThrowsAsync<CatalogValidationException>(() =>
            service.BrowseAsync(Guid.NewGuid(), new() { Type = "Inexistente" }, CancellationToken.None));
        Assert.All(CardTypes.Prefixes.Keys, type => Assert.Contains(type, error.Errors["type"]));
    }

    [Theory]
    [InlineData(-1, 12, "after")]
    [InlineData(0, 0, "pageSize")]
    [InlineData(0, 101, "pageSize")]
    public async Task Invalid_pagination_is_rejected(long after, int pageSize, string field)
    {
        var service = new PlayerCatalogService(new RecordingRepository());
        var error = await Assert.ThrowsAsync<CatalogValidationException>(() =>
            service.BrowseAsync(Guid.NewGuid(), new() { After = after, PageSize = pageSize }, CancellationToken.None));
        Assert.Contains(field, error.Errors.Keys);
    }

    [Fact]
    public async Task Collection_forces_ownership_and_uses_identity_from_token()
    {
        var playerId = Guid.NewGuid();
        var repository = new RecordingRepository();
        var controller = new PlayerCatalogController(new PlayerCatalogService(repository))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", playerId.ToString())], "test"))
                }
            }
        };
        var response = await controller.Collection(new() { Type = "Fuego", Cost = 3, OwnedOnly = false }, CancellationToken.None);
        Assert.IsType<OkObjectResult>(response);
        Assert.Equal(playerId, repository.PlayerId);
        Assert.True(repository.Query!.OwnedOnly);
        Assert.Equal("Fuego", repository.Query.Type);
        Assert.Equal(3, repository.Query.Cost);
    }

    private sealed class RecordingRepository : IPlayerCatalogRepository
    {
        public PlayerCatalogQuery? Query { get; private set; }
        public Guid PlayerId { get; private set; }

        public Task<PlayerCatalogPage> BrowseAsync(Guid playerId, PlayerCatalogQuery query, CancellationToken cancellationToken)
        {
            PlayerId = playerId;
            Query = query;
            return Task.FromResult(new PlayerCatalogPage([], null, 0));
        }
    }
}
