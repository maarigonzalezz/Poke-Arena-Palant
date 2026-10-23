
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PokeArena.Application.Cards;

namespace PokeArena.Api.Controllers;

/// <summary>Expone la búsqueda de cartas para usuarios con rol ContentAdmin.</summary>
[ApiController]
[Route("api/cards")]
[Authorize(Roles = "ContentAdmin")]
public sealed class CardsController : ControllerBase
{
    private readonly ICardSearchRepository _repository;

    public CardsController(
        ICardSearchRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Valida los filtros y consulta una sesión de búsqueda del usuario autenticado.</summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] CardSearchQuery query,
        CancellationToken cancellationToken)
    {
        var errors = CardSearchValidator.Validate(query);

        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                type = "invalid_search_criteria",
                message = "Criterios de búsqueda inválidos.",
                errors
            });
        }

        var actorId = User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier
        )?.Value;

        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Unauthorized(new
            {
                type = "authentication_required",
                message = "No se pudo identificar al usuario."
            });
        }

        try
        {
            var result = await _repository.SearchAsync(
                actorId,
                query,
                cancellationToken);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                type = "search_not_found",
                message = ex.Message
            });
        }
    }
}
