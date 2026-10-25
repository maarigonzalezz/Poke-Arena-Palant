using System.Text.Json;
using Npgsql;
using PokeArena.Application.Cards;

namespace PokeArena.Infrastructure.Cards;

public sealed class PostgresAbilityRepository(NpgsqlDataSource dataSource) : IAbilityRepository
{
    public async Task<IReadOnlyList<AbilityItem>> GetAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT code, name, trigger_type, effect_type, parameters::text, restricted_card_type
            FROM abilities WHERE active = TRUE ORDER BY code
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<AbilityItem>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(reader.GetString(0), reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3),
                JsonSerializer.Deserialize<JsonElement>(reader.GetString(4)), reader.IsDBNull(5) ? null : reader.GetString(5)));
        return result;
    }
}
