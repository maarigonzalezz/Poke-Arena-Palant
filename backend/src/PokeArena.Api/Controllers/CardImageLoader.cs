
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using PokeArena.Application.Cards;

namespace PokeArena.Api.Controllers;
public static class CardImageLoader
{
    public static async Task<CardFileImage> ReadAsync(
    IFormFile file,
    CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var info = await Image.IdentifyAsync(stream, cancellationToken);

        var width = info.Width;
        var height = info.Height;
        var divisor = GreatestCommonDivisor(width, height);

        var detectedFormat = info.Metadata.DecodedImageFormat?.Name ?? string.Empty;
        var format = detectedFormat.ToUpperInvariant() switch
        {
            "JPEG" => "JPG",
            "PNG" => "PNG",
            "WEBP" => "WebP",
            _ => detectedFormat
        };

        return new CardFileImage
        {
            Name = Path.GetFileName(file.FileName),
            Format = format,
            hProportion = width / divisor,
            vProportion = height / divisor,
            hPixels = width,
            vPixels = height
        };
    }

    private static int GreatestCommonDivisor(int left, int right)
    {
        while (right != 0)
        {
            (left, right) = (right, left % right);
        }

        return left;
    }
}