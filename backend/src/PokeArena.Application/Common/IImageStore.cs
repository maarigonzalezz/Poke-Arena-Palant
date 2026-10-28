namespace PokeArena.Application.Common;

// Representa los bytes recibidos sin depender de IFormFile ni de HTTP.
public sealed record ImageUpload(byte[] Content);
// Separa URLs públicas de la carpeta física utilizada para limpiar archivos.
public sealed record StoredImage(string OriginalUrl, string SmallUrl, string Directory);

/// <summary>Guarda ambas imágenes antes de publicar el registro en el catálogo.</summary>
public interface IImageStore
{
    Task<StoredImage> SaveAsync(ImageUpload upload, string kind, CancellationToken cancellationToken);
    void Delete(StoredImage image);
}
