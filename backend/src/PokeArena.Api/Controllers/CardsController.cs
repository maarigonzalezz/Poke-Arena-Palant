
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
    private readonly ICardCreateRepository _create;

    public CardsController(
        ICardSearchRepository repository,
        ICardCreateRepository create
    )
    {
        _repository = repository;
        _create = create;
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

    /// <summary>Valida los filtros y consulta una sesión de creacion del usuario autenticado.</summary>
    [HttpPost]
    public async Task<IActionResult> Create (
        [FromQuery] CardCreateQuery query,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken
    ) {
        // Cargar imagen
        CardFileImage image = await CardImageLoader.ReadAsync(file, cancellationToken);

        // Validar query
        var errors = CardCreateValidator.Validate(query, image);

        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                type = "invalid_create_criteria",
                message = "Criterios de creacion inválidos.",
                errors
            });
        }

        // Validar usuario
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

        // Crear la carta y almacenar su imagen original.
        try
        {
            await using var imageContent = file.OpenReadStream();
            var result = await _create.CreateAsync(
                actorId,
                query,
                image,
                imageContent,
                cancellationToken);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new
            {
                type = "create_not_possible",
                message = ex.Message
            });
        }
    }
}
