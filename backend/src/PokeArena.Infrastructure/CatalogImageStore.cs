using PokeArena.Application.Common;
using SkiaSharp;

namespace PokeArena.Infrastructure;

/// <summary>Valida el contenido y guarda original y miniatura en una carpeta única.</summary>
public sealed class CatalogImageStore(string rootDirectory) : IImageStore
{
    public async Task<StoredImage> SaveAsync(ImageUpload upload, string kind, CancellationToken cancellationToken)
    {
        if (upload.Content.Length is 0 or > 5 * 1024 * 1024)
            throw Invalid("La imagen debe pesar entre 1 byte y 5 MB.");
        if (kind is not "cards" and not "locations") throw new ArgumentException("Tipo de imagen inválido.");

        using var data = SKData.CreateCopy(upload.Content);
        using var codec = SKCodec.Create(data);
        if (codec is null) throw Invalid("El archivo no contiene una imagen válida.");
        var extension = codec.EncodedFormat switch
        { SKEncodedImageFormat.Png => "png", SKEncodedImageFormat.Jpeg => "jpg", SKEncodedImageFormat.Webp => "webp", _ => null };
        if (extension is null) throw Invalid("La imagen debe ser PNG, JPG o WebP.");
        var card = kind == "cards";
        var width = card ? 600 : 800;
        var height = card ? 840 : 600;
        if (codec.Info.Width < width || codec.Info.Height < height ||
            Math.Abs((double)codec.Info.Width / codec.Info.Height - (double)width / height) > 0.001)
            throw Invalid($"La imagen requiere al menos {width} × {height} y proporción {(card ? "5:7" : "4:3")}.");
        if (codec.FrameCount > 1) throw Invalid("Seleccione una imagen estática de un solo cuadro.");
        using var image = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (image.GetPixels() == IntPtr.Zero || codec.GetPixels(image.Info, image.GetPixels()) != SKCodecResult.Success)
            throw Invalid("La imagen está incompleta o no se pudo decodificar.");
        cancellationToken.ThrowIfCancellationRequested();

        var id = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(rootDirectory, kind, id);
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(directory, $"original.{extension}"), upload.Content, cancellationToken);
            using var small = image.Resize(new SKImageInfo(card ? 200 : 320, card ? 280 : 240), new SKSamplingOptions(SKFilterMode.Linear));
            if (small is null) throw new IOException("No se pudo generar la miniatura.");
            using var encoded = small.Encode(SKEncodedImageFormat.Png, 100);
            await File.WriteAllBytesAsync(Path.Combine(directory, "small.png"), encoded.ToArray(), cancellationToken);
            return new StoredImage($"/files/{kind}/{id}/original.{extension}", $"/files/{kind}/{id}/small.png", directory);
        }
        catch { Directory.Delete(directory, recursive: true); throw; }
    }

    public void Delete(StoredImage image)
    {
        var root = Path.GetFullPath(rootDirectory) + Path.DirectorySeparatorChar;
        var directory = Path.GetFullPath(image.Directory);
        if (!directory.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Ruta fuera del almacenamiento.");
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private static CatalogValidationException Invalid(string message) => new(new() { ["image"] = message });
}
