using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PokeArena.Application.Common;
using PokeArena.Infrastructure.Persistence;

namespace PokeArena.Infrastructure.Catalog;

/// <summary>Confirma entidades, versiones y respuesta idempotente mediante una transacción de EF.</summary>
public sealed class CatalogTransaction(PokeArenaDbContext db) : ICatalogTransaction
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // La huella conserva el formato existente para aceptar reintentos anteriores a EF.
    private static string Fingerprint(object data, ImageUpload image) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        { data, imageHash = Convert.ToHexString(SHA256.HashData(image.Content)) }, Json)));

    public async Task<T> RunAsync<T>(string actor, string key, string operation, object data, ImageUpload image,
        Func<ICatalogWriteSession, Task<T>> create, CancellationToken cancellationToken)
    {
        var hash = Fingerprint(data, image);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // ON CONFLICT reserva la clave sin abortar la transacción cuando llega un reintento.
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO idempotency_requests(actor_id, operation, idempotency_key, request_hash, status)
                VALUES ({actor}, {operation}, {key}, {hash}, 'Processing') ON CONFLICT DO NOTHING
                """, cancellationToken);
            // El bloqueo serializa solicitudes con la misma clave; los valores son parámetros SQL.
            var request = await db.IdempotencyRequests.FromSql($"""
                SELECT * FROM idempotency_requests
                WHERE actor_id={actor} AND operation={operation} AND idempotency_key={key} FOR UPDATE
                """).SingleAsync(cancellationToken);
            if (request.RequestHash != hash)
                throw new CatalogConflictException("La clave de idempotencia ya fue utilizada con otros datos.");
            if (request.ResponseBody is not null)
            {
                var replay = JsonSerializer.Deserialize<T>(request.ResponseBody, Json)!;
                await transaction.CommitAsync(cancellationToken);
                return replay;
            }

            var result = await create(new PostgresCatalogWriteSession(db));
            request.Status = "Completed";
            request.ResponseStatus = 201;
            request.ResponseBody = JsonSerializer.Serialize(result, Json);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        finally
        {
            // Evita reutilizar entidades de una operación fallida dentro del mismo contexto.
            db.ChangeTracker.Clear();
        }
    }
}
