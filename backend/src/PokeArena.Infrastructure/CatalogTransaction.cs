using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using PokeArena.Application.Common;

namespace PokeArena.Infrastructure;

/// <summary>Serializa reintentos de la misma operación y confirma datos y respuesta juntos.</summary>
public sealed class CatalogTransaction(NpgsqlDataSource dataSource)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Fingerprint<T>(T data, ImageUpload image) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        { data, imageHash = Convert.ToHexString(SHA256.HashData(image.Content)) }, Json)));

    public async Task<T> RunAsync<T>(string actor, string key, string operation, string hash,
        Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> create, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100)
            throw new CatalogValidationException(new() { ["idempotencyKey"] = "Idempotency-Key es obligatorio y admite hasta 100 caracteres." });
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // La restricción única hace esperar al segundo envío hasta confirmar o revertir el primero.
        await using (var reserve = new NpgsqlCommand("""
            INSERT INTO idempotency_requests(actor_id, operation, idempotency_key, request_hash, status)
            VALUES (@actor, @operation, @key, @hash, 'Processing') ON CONFLICT DO NOTHING
            """, connection, transaction))
        {
            Bind(reserve, actor, key, operation);
            reserve.Parameters.AddWithValue("hash", hash);
            await reserve.ExecuteNonQueryAsync(cancellationToken);
        }
        T result;
        await using (var read = new NpgsqlCommand("""
            SELECT request_hash, response_body::text FROM idempotency_requests
            WHERE actor_id=@actor AND operation=@operation AND idempotency_key=@key FOR UPDATE
            """, connection, transaction))
        {
            Bind(read, actor, key, operation);
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            if (reader.GetString(0) != hash)
                throw new CatalogConflictException("La clave de idempotencia ya fue utilizada con otros datos.");
            if (!reader.IsDBNull(1)) return JsonSerializer.Deserialize<T>(reader.GetString(1), Json)!;
        }
        result = await create(connection, transaction);
        await using (var finish = new NpgsqlCommand("""
            UPDATE idempotency_requests SET status='Completed', response_status=201, response_body=@response::jsonb
            WHERE actor_id=@actor AND operation=@operation AND idempotency_key=@key
            """, connection, transaction))
        {
            Bind(finish, actor, key, operation);
            finish.Parameters.AddWithValue("response", JsonSerializer.Serialize(result, Json));
            await finish.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static void Bind(NpgsqlCommand command, string actor, string key, string operation)
    {
        command.Parameters.AddWithValue("actor", actor);
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("operation", operation);
    }
}
