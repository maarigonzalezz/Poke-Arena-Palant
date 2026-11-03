using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PokeArena.Application.Cards;

namespace PokeArena.Api.Controllers;

/// <summary>Consulta y creación del catálogo administrativo.</summary>
[ApiController]
[Route("api/cards")]
[Authorize(Roles = "ContentAdmin")]
public sealed class CardsController(CardService cards) : ControllerBase
{
    // GET: valida filtros y entrega una página de la búsqueda del administrador.
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] CardSearchQuery query, CancellationToken cancellationToken)
    {
        // Identifica al dueño de la sesión de búsqueda a partir del JWT.
        var actor = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Unauthorized(new { message="No se pudo identificar al usuario." });
        try { return Ok(await cards.SearchAsync(actor, query, cancellationToken)); }
        // Una sesión inexistente o vencida se informa como 404.
        catch (KeyNotFoundException ex) { return NotFound(new { type="search_not_found", message=ex.Message }); }
    }

    // POST: traduce HTTP; el servicio coordina validación, imágenes y persistencia.
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm] CreateCardForm form,
        [FromHeader(Name="Idempotency-Key")] string? key, CancellationToken cancellationToken)
    {
        var actor = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Unauthorized(new { message="No se pudo identificar al usuario." });
        var image = await CatalogUpload.ReadAsync(form.Image, Request.Form.Files.Count, cancellationToken);
        var result = await cards.CreateAsync(actor, new CardCreateQuery
        {
            Name=form.Name, Cost=form.Cost!.Value, Power=form.Power!.Value,
            Type=form.Type, Rarity=form.Rarity, AbilityCode=form.AbilityCode
        }, image, key ?? "", cancellationToken);
        // 201 confirma la creación y devuelve la carta con sus rutas de imagen.
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
