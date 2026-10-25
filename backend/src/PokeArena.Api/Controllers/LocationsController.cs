using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PokeArena.Application.Locations;
using PokeArena.Domain.Entities;

namespace PokeArena.Api.Controllers;

/// <summary>
/// Expone el endpoint para crear ubicaciones del catálogo.
/// </summary>
/// <remarks>
/// Todas las operaciones de este controlador requieren un usuario autenticado
/// con el rol <c>ContentAdmin</c>.
/// </remarks>
[ApiController]
[Route("api/locations")]
[Authorize(Roles = "ContentAdmin")]
public sealed class LocationsController : ControllerBase
{
    private readonly ILocationRepository _repository;

    /// <summary>
    /// Inicializa el controlador con el repositorio de ubicaciones.
    /// </summary>
    /// <param name="repository">Servicio que persiste las ubicaciones.</param>
    public LocationsController(ILocationRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Valida y crea una ubicación junto con su versión inicial.
    /// </summary>
    /// <param name="request">Campos recibidos desde el formulario multipart.</param>
    /// <param name="cancellationToken">Token para cancelar la operación.</param>
    /// <returns>
    /// <c>400 Bad Request</c> con los errores si la solicitud no es válida;
    /// <c>201 Created</c> con la ubicación persistida si se crea correctamente.
    /// </returns>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create(
        [FromForm] CreateLocationRequest request,
        CancellationToken cancellationToken)
    {
        var errors = LocationValidator.Validate(request);

        if (errors.Count > 0)
        {
            return BadRequest(new
            {
                type = "invalid_location",
                message = "Los datos de la ubicación son inválidos.",
                errors
            });
        }

        var location = new Location
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            MaxCards = request.MaxCards,
            EnergyMode = request.EnergyMode,
            ExtraEnergy = request.ExtraEnergy,
            EffectType = request.EffectType,
            EffectParameters = request.EffectParameters,
            ImageStatus = "Pending",
            CurrentVersion = 1,
            Active = true
        };

        var created = await _repository.CreateAsync(
            location,
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, created);
    }
}
