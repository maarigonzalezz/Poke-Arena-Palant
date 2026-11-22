using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PokeArena.Application.Catalog;
using PokeArena.Infrastructure.Authentication;

namespace PokeArena.Api.Controllers;

//Expone catalogo y coleccion mediante el mismo caso de uso autenticado.
[ApiController]
[Route("api/player")]
[Authorize(AuthenticationSchemes = PlatformTokenOptions.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PlayerCatalogController(PlayerCatalogService catalog) : ControllerBase
{
    [HttpGet("catalog")]
    public Task<IActionResult> Browse([FromQuery] PlayerCatalogQuery query, CancellationToken cancellationToken) =>
        ReadAsync(query, cancellationToken);

    [HttpGet("collection")]
    public Task<IActionResult> Collection([FromQuery] PlayerCatalogQuery query, CancellationToken cancellationToken) =>
        ReadAsync(query with { OwnedOnly = true }, cancellationToken);

    private async Task<IActionResult> ReadAsync(PlayerCatalogQuery query, CancellationToken cancellationToken)
    {
        // Nunca recibe el propietario desde la URL ni desde un formulario del cliente.
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var playerId) || playerId == Guid.Empty)
            return Unauthorized();
        return Ok(await catalog.BrowseAsync(playerId, query, cancellationToken));
    }
}
