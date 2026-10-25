using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PokeArena.Application.Cards;

namespace PokeArena.Api.Controllers;

/// <summary>Consulta y creación del catálogo administrativo.</summary>
[ApiController]
[Route("api/cards")]
[Authorize(Roles = "ContentAdmin")]
public sealed class CardsController(ICardSearchRepository search, ICardCreateRepository create) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] CardSearchQuery query, CancellationToken cancellationToken)
    {
        var errors = CardSearchValidator.Validate(query);
        if (errors.Count > 0) return BadRequest(new { type="invalid_search_criteria", message="Criterios de búsqueda inválidos.", errors });
        var actor = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Unauthorized(new { message="No se pudo identificar al usuario." });
        try { return Ok(await search.SearchAsync(actor, query, cancellationToken)); }
        catch (KeyNotFoundException ex) { return NotFound(new { type="search_not_found", message=ex.Message }); }
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm] CreateCardForm form,
        [FromHeader(Name="Idempotency-Key")] string? key, CancellationToken cancellationToken)
    {
        var actor = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Unauthorized(new { message="No se pudo identificar al usuario." });
        var image = await CatalogUpload.ReadAsync(form.Image, Request.Form.Files.Count, cancellationToken);
        var result = await create.CreateAsync(actor, new CardCreateQuery
        {
            Name=form.Name, Cost=form.Cost!.Value, Power=form.Power!.Value,
            Type=form.Type, Rarity=form.Rarity, AbilityCode=form.AbilityCode
        }, image, key ?? "", cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
