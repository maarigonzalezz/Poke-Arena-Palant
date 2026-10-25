using System.ComponentModel.DataAnnotations;
using PokeArena.Application.Common;

namespace PokeArena.Api.Controllers;

// Los números anulables permiten distinguir un campo omitido del valor válido cero.
public sealed class CreateCardForm
{
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [Required, Range(0, 10)] public int? Cost { get; set; }
    [Required, Range(-20, 20)] public int? Power { get; set; }
    [Required] public string Type { get; set; } = "";
    [Required] public string Rarity { get; set; } = "";
    [Required] public string AbilityCode { get; set; } = "";
    [Required] public IFormFile? Image { get; set; }
}

public sealed class CreateLocationForm
{
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [Required, StringLength(10000)] public string Description { get; set; } = "";
    [Required, Range(0, 5)] public int? MaxCards { get; set; }
    [Required] public string EnergyMode { get; set; } = "";
    [Required, Range(0, 3)] public int? ExtraEnergy { get; set; }
    [Required] public string EffectType { get; set; } = "";
    [Required, StringLength(4096)] public string EffectParameters { get; set; } = "{}";
    [Required] public IFormFile? Image { get; set; }
}

internal static class CatalogUpload
{
    public static async Task<ImageUpload> ReadAsync(IFormFile? file, int count, CancellationToken cancellationToken)
    {
        if (file is null || count != 1 || file.Length is 0 or > 5 * 1024 * 1024)
            throw new CatalogValidationException(new() { ["image"] = "Suba una sola imagen no vacía de hasta 5 MB." });
        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        return new ImageUpload(buffer.ToArray());
    }
}
