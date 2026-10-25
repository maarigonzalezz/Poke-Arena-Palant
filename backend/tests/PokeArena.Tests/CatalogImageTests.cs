using PokeArena.Application.Common;
using PokeArena.Infrastructure;
using SkiaSharp;

namespace PokeArena.Tests;

public sealed class CatalogImageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "pokearena-images-" + Guid.NewGuid().ToString("N"));

    public static byte[] ImageBytes(int width=600, int height=840, SKEncodedImageFormat format=SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(width,height);
        bitmap.Erase(SKColors.OrangeRed);
        using var data = bitmap.Encode(format,90);
        return data.ToArray();
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Png)] [InlineData(SKEncodedImageFormat.Jpeg)] [InlineData(SKEncodedImageFormat.Webp)]
    public async Task Saves_original_and_exact_thumbnail(SKEncodedImageFormat format)
    {
        var bytes = ImageBytes(format:format);
        var store = new CatalogImageStore(root);
        var image = await store.SaveAsync(new(bytes), "cards", default);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(image.Directory, Path.GetFileName(image.OriginalUrl))));
        using var small = SKBitmap.Decode(Path.Combine(image.Directory,"small.png"));
        Assert.Equal(200,small.Width); Assert.Equal(280,small.Height);
        store.Delete(image);
        Assert.False(Directory.Exists(image.Directory));
    }

    [Fact] public async Task Saves_location_thumbnail()
    {
        var image = await new CatalogImageStore(root).SaveAsync(new(ImageBytes(800,600)),"locations",default);
        using var small = SKBitmap.Decode(Path.Combine(image.Directory,"small.png"));
        Assert.Equal(320,small.Width); Assert.Equal(240,small.Height);
    }

    [Fact] public async Task Rejects_corruption_size_format_and_dimensions_without_files()
    {
        var store = new CatalogImageStore(root);
        foreach (var bytes in new[] { Array.Empty<byte>(), new byte[5*1024*1024+1], new byte[] {1,2,3}, ImageBytes(100,140), ImageBytes(840,600), ImageBytes(800,800) })
            await Assert.ThrowsAsync<CatalogValidationException>(() => store.SaveAsync(new(bytes),"cards",default));
        Assert.False(Directory.Exists(root));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root,true); }
}
