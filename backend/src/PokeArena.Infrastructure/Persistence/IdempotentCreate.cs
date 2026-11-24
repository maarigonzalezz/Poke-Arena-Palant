using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace PokeArena.Infrastructure.Persistence;

internal static class IdempotentCreate
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static string Fingerprint(object payload) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(payload, Json)));

    internal static async Task<T> RunAsync<T>(PokeArenaDbContext db, string actor, string key, string operation,
        string hash, Func<Exception> conflictingKey, Func<Task<T>> create, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        try
        {
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO idempotency_requests(actor_id, operation, idempotency_key, request_hash, status)
                VALUES ({actor}, {operation}, {key}, {hash}, 'Processing') ON CONFLICT DO NOTHING
                """, token);
            // El bloqueo serializa solicitudes simultáneas con la misma clave.
            var request = await db.IdempotencyRequests.FromSql($"""
                SELECT * FROM idempotency_requests
                WHERE actor_id={actor} AND operation={operation} AND idempotency_key={key} FOR UPDATE
                """).SingleAsync(token);
            if (request.RequestHash != hash) throw conflictingKey();

            if (request.ResponseBody is not null)
            {
                var replay = JsonSerializer.Deserialize<T>(request.ResponseBody, Json)!;
                await transaction.CommitAsync(token);
                return replay;
            }

            var result = await create();
            request.Status = "Completed";
            request.ResponseStatus = 201;
            request.ResponseBody = JsonSerializer.Serialize(result, Json);
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return result;
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }
}
