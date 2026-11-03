using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PokeArena.Application.Locations;

namespace PokeArena.Api.Controllers;

// Conecta POST /api/locations con el contrato de creación de Application.
[ApiController]
[Route("api/locations")]
[Authorize(Roles = "ContentAdmin")]
public sealed class LocationsController(LocationService locations) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm] CreateLocationForm form,
        [FromHeader(Name="Idempotency-Key")] string? key, CancellationToken cancellationToken)
    {
        var actor = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Unauthorized(new { message="No se pudo identificar al usuario." });
        // Verifica el archivo recibido antes de ejecutar las reglas de creación.
        var image = await CatalogUpload.ReadAsync(form.Image, Request.Form.Files.Count, cancellationToken);
        // Traduce el formulario HTTP al contrato que entiende Application.
        var result = await locations.CreateAsync(actor, new CreateLocationRequest
        {
            Name=form.Name, Description=form.Description, MaxCards=form.MaxCards!.Value,
            EnergyMode=form.EnergyMode, ExtraEnergy=form.ExtraEnergy!.Value,
            EffectType=form.EffectType, EffectParameters=form.EffectParameters
        }, image, key ?? "", cancellationToken);
        // Devuelve 201 con la ubicación persistida y sus imágenes.
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
