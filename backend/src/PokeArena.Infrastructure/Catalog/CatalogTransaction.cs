using System.Security.Cryptography;
using PokeArena.Application.Common;
using PokeArena.Infrastructure.Persistence;

namespace PokeArena.Infrastructure.Catalog;

/// <summary>Confirma entidades, versiones y respuesta idempotente mediante una transacción de EF.</summary>
public sealed class CatalogTransaction(PokeArenaDbContext db) : ICatalogTransaction
{
    public Task<T> RunAsync<T>(string actor, string key, string operation, object data, ImageUpload image,
        Func<ICatalogWriteSession, Task<T>> create, CancellationToken cancellationToken)
    {
        // Conserva la huella de las solicitudes que catálogo ya tiene guardadas.
        var hash = IdempotentCreate.Fingerprint(new
        { data, imageHash = Convert.ToHexString(SHA256.HashData(image.Content)) });
        return IdempotentCreate.RunAsync(db, actor, key, operation, hash,
            () => new CatalogConflictException("La clave de idempotencia ya fue utilizada con otros datos."),
            () => create(new PostgresCatalogWriteSession(db)), cancellationToken);
    }
}
