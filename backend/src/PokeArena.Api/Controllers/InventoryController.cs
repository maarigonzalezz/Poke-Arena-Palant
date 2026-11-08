using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using PokeArena.Application.Inventory;

namespace PokeArena.Api.Controllers;

/// <summary>Accede al inventario de un jugador; que incluye sus cartas y sus monedas virtuales.</summary>
[ApiController]
[Route("api/player/inventory")]
public sealed class InventoryController(InventoryService inventory) : ControllerBase
{
    [HttpGet("owned")]
    public async Task<IActionResult> Consult(
        [FromQuery] PlayerProfileQuery query,
        CancellationToken cancellationToken
    ){
        return Ok();
    }
    // [HttpGet("collection")]
    // [HttpPost("collection/add")]
    // [HttpPost("currency/add")]
}