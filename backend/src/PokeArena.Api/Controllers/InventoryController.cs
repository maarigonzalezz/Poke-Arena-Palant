using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PokeArena.Application.Inventory;
using PokeArena.Infrastructure.Authentication;

namespace PokeArena.Api.Controllers;

// El token propio identifica al dueño; no se admiten perfiles elegidos por el cliente.
[ApiController]
[Route("api/player/inventory")]
[Authorize(AuthenticationSchemes = PlatformTokenOptions.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InventoryController(InventoryService inventory) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Consult(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var playerId) || playerId == Guid.Empty)
            return Unauthorized();
        return Ok(await inventory.ConsultAsync(playerId, cancellationToken));
    }

    // El historial expone los movimientos del jugador autenticado, en páginas de cincuenta.
    [HttpGet("cube-history")]
    public async Task<IActionResult> History([FromQuery] int after = 0, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var playerId) || playerId == Guid.Empty)
            return Unauthorized();
        if (after < 0) return BadRequest(new { message = "after debe ser cero o positivo." });
        return Ok(await inventory.GetCubeHistoryAsync(playerId, after, cancellationToken));
    }
}
