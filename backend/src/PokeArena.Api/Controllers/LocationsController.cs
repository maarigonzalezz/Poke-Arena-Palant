using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PokeArena.Application.Locations;

namespace PokeArena.Api.Controllers;

[ApiController]
[Route("api/locations")]
[Authorize(Roles = "ContentAdmin")]
public sealed class LocationsController(ILocationRepository repository) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm] CreateLocationForm form,
        [FromHeader(Name="Idempotency-Key")] string? key, CancellationToken cancellationToken)
    {
        var actor = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actor)) return Unauthorized(new { message="No se pudo identificar al usuario." });
        var image = await CatalogUpload.ReadAsync(form.Image, Request.Form.Files.Count, cancellationToken);
        var result = await repository.CreateAsync(actor, new CreateLocationRequest
        {
            Name=form.Name, Description=form.Description, MaxCards=form.MaxCards!.Value,
            EnergyMode=form.EnergyMode, ExtraEnergy=form.ExtraEnergy!.Value,
            EffectType=form.EffectType, EffectParameters=form.EffectParameters
        }, image, key ?? "", cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
