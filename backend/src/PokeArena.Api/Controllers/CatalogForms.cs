using System.ComponentModel.DataAnnotations;
using PokeArena.Application.Common;

namespace PokeArena.Api.Controllers;

// Los números anulables permiten distinguir un campo omitido del valor válido cero.
public sealed class CreateCardForm
{
    [Required] public string Name { get; set; } = "";
    [Required] public int? Cost { get; set; }
    [Required] public int? Power { get; set; }
    [Required] public string Type { get; set; } = "";
    [Required] public string Rarity { get; set; } = "";
    [Required] public string AbilityCode { get; set; } = "";
    [Required] public IFormFile? Image { get; set; }
}

// HTTP comprueba presencia y formato; Application decide longitudes y rangos permitidos.
public sealed class CreateLocationForm
{
    [Required] public string Name { get; set; } = "";
    [Required] public string Description { get; set; } = "";
    [Required] public int? MaxCards { get; set; }
    [Required] public string EnergyMode { get; set; } = "";
    [Required] public int? ExtraEnergy { get; set; }
    [Required] public string EffectType { get; set; } = "";
    [Required] public string EffectParameters { get; set; } = "{}";
    [Required] public IFormFile? Image { get; set; }
}

internal static class CatalogUpload
{
    public static async Task<ImageUpload> ReadAsync(IFormFile? file, int count, CancellationToken cancellationToken)
    {
        // Rechaza archivos vacíos, múltiples o mayores de 5 MB.
        if (file is null || count != 1 || file.Length is 0 or > 5 * 1024 * 1024)
            throw new CatalogValidationException(new() { ["image"] = "Suba una sola imagen no vacía de hasta 5 MB." });
        // Convierte el archivo HTTP en bytes para no acoplar Application a ASP.NET.
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        return new ImageUpload(buffer.ToArray());
    }
}
