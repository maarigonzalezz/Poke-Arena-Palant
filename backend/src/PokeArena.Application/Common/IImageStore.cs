namespace PokeArena.Application.Common;

public sealed record ImageUpload(byte[] Content);
public sealed record StoredImage(string OriginalUrl, string SmallUrl, string Directory);

/// <summary>Guarda ambas imágenes antes de publicar el registro en el catálogo.</summary>
public interface IImageStore
{
    Task<StoredImage> SaveAsync(ImageUpload upload, string kind, CancellationToken cancellationToken);
    void Delete(StoredImage image);
}
