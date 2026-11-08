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
    [HttpGet]
    public async Task<IActionResult> Consult(
        [FromQuery] PlayerProfileQuery query,
        CancellationToken cancellationToken
    ){
        try
        {
            return Ok(await inventory.ConsultAsync(query, cancellationToken));
        }
        catch (KeyNotFoundException e)
        {
            return NotFound(e.Message);
        }
        catch (InventoryValidationException e)
        {
            return BadRequest(e.Message);
        }
        catch (InventoryConflictException e)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: e.Message,
                detail: e.Source
            );
        }
    }
    // [HttpGet("collection")]
    // [HttpPost("collection/add")]
}