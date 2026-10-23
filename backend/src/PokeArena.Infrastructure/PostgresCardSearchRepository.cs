
using System.Text;
using Npgsql;
using PokeArena.Application.Cards;

namespace PokeArena.Infrastructure.Cards;

/// <summary>Combina filtros SQL y conserva el orden de los resultados en sesiones de 30 minutos.</summary>
public sealed class PostgresCardSearchRepository
    : ICardSearchRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresCardSearchRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <summary>Crea una sesión o lee la siguiente página de una sesión perteneciente al usuario.</summary>
    public async Task<CardSearchResult> SearchAsync(
        string actorId,
        CardSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var errors = CardSearchValidator.Validate(query);

        if (errors.Count > 0)
        {
            throw new ArgumentException(
                string.Join(
                    "; ",
                    errors.Select(
                        e => $"{e.Key}: {e.Value}")
                )
            );
        }

        if (query.SearchId.HasValue)
        {
            return await ReadPageAsync(
                actorId,
                query.SearchId.Value,
                query.After,
                query.PageSize,
                cancellationToken);
        }

        var searchId = Guid.NewGuid();

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        await using var transaction =
            await connection.BeginTransactionAsync(
                cancellationToken);

        const string createSessionSql = """
            INSERT INTO card_search_sessions (
                id,
                actor_id,
                expires_at
            )
            VALUES (
                @id,
                @actor,
                NOW() + INTERVAL '30 minutes'
            )
            """;

        await using (var create = new NpgsqlCommand(
            createSessionSql,
            connection,
            transaction))
        {
            create.Parameters.AddWithValue("id", searchId);
            create.Parameters.AddWithValue("actor", actorId);

            await create.ExecuteNonQueryAsync(
                cancellationToken);
        }

        // Fija los identificadores y su orden; los datos de cada carta se leen al paginar.
        var sql = new StringBuilder("""
            INSERT INTO card_search_items (
                session_id,
                position,
                card_id
            )
            SELECT
                @sessionId,
                ROW_NUMBER() OVER (ORDER BY c.id),
                c.id
            FROM cards c
            INNER JOIN abilities a
                ON a.code = c.ability_code
            WHERE c.active = TRUE
            """);

        var parameters = new List<NpgsqlParameter>
        {
            new("sessionId", searchId)
        };

        AddFilter(sql, parameters,
            query.Code, "c.code", "code");

        AddNameFilter(sql, parameters, query.Name);

        AddNumberFilter(
            sql, parameters, query.Cost, "c.cost", "cost");

        AddMinFilter(
            sql, parameters, query.MinCost, "c.cost", "minCost");

        AddMaxFilter(
            sql, parameters, query.MaxCost, "c.cost", "maxCost");

        AddNumberFilter(
            sql, parameters, query.Power, "c.power", "power");

        AddMinFilter(
            sql, parameters, query.MinPower, "c.power", "minPower");

        AddMaxFilter(
            sql, parameters, query.MaxPower, "c.power", "maxPower");

        AddFilter(sql, parameters,
            query.Type, "c.card_type", "type");

        AddFilter(sql, parameters,
            query.Rarity, "c.rarity", "rarity");

        AddFilter(sql, parameters,
            query.AbilityCode, "a.code", "abilityCode");

        AddFilter(sql, parameters,
            query.AbilityTrigger,
            "a.trigger_type", "abilityTrigger");

        AddFilter(sql, parameters,
            query.AbilityEffect,
            "a.effect_type", "abilityEffect");

        await using (var insert = new NpgsqlCommand(
            sql.ToString(),
            connection,
            transaction))
        {
            insert.Parameters.AddRange(
                parameters.ToArray());

            await insert.ExecuteNonQueryAsync(
                cancellationToken);
        }

        const string updateCountSql = """
            UPDATE card_search_sessions
            SET result_count = (
                SELECT COUNT(*)
                FROM card_search_items
                WHERE session_id = @id
            )
            WHERE id = @id
            """;

        await using (var update = new NpgsqlCommand(
            updateCountSql,
            connection,
            transaction))
        {
            update.Parameters.AddWithValue("id", searchId);

            await update.ExecuteNonQueryAsync(
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return await ReadPageAsync(
            actorId,
            searchId,
            0,
            query.PageSize,
            cancellationToken);
    }

    // Verifica propietario y vigencia; LEFT JOIN conserva las sesiones sin resultados.
    private async Task<CardSearchResult> ReadPageAsync(
        string actorId,
        Guid searchId,
        long after,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        const string sql = """
            SELECT
                s.result_count,
                i.position,
                c.code,
                c.name,
                c.cost,
                c.power,
                c.card_type,
                c.rarity,
                c.image_status,
                c.image_original_path,
                a.code,
                a.name,
                a.trigger_type,
                a.effect_type,
                a.parameters::text
            FROM card_search_sessions s
            LEFT JOIN card_search_items i
                ON i.session_id = s.id
                AND i.position > @after
                AND i.position <= @after + @limit
            LEFT JOIN cards c
                ON c.id = i.card_id
            LEFT JOIN abilities a
                ON a.code = c.ability_code
            WHERE s.id = @searchId
              AND s.actor_id = @actor
              AND s.expires_at > NOW()
            ORDER BY i.position
            """;

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("searchId", searchId);
        command.Parameters.AddWithValue("actor", actorId);
        command.Parameters.AddWithValue("after", after);
        command.Parameters.AddWithValue("limit", pageSize);

        var items = new List<CardSearchItem>();
        long totalCount = 0;
        long lastPosition = after;
        bool foundSession = false;

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            foundSession = true;
            totalCount = reader.GetInt64(0);

            if (reader.IsDBNull(1))
                continue;

            lastPosition = reader.GetInt64(1);

            items.Add(new CardSearchItem(
                Code: reader.GetString(2),
                Name: reader.GetString(3),
                Cost: reader.GetInt32(4),
                Power: reader.GetInt32(5),
                Type: reader.GetString(6),
                Rarity: reader.GetString(7),
                ImageStatus: reader.GetString(8),
                ImageUrl: reader.IsDBNull(9)
                    ? null
                    : reader.GetString(9),
                AbilityCode: reader.GetString(10),
                AbilityName: reader.GetString(11),
                AbilityTrigger: reader.IsDBNull(12)
                    ? null
                    : reader.GetString(12),
                AbilityEffect: reader.IsDBNull(13)
                    ? null
                    : reader.GetString(13),
                AbilityParameters: reader.GetString(14)
            ));
        }

        if (!foundSession)
        {
            throw new KeyNotFoundException(
                "La búsqueda no existe o ha expirado.");
        }

        long? nextPosition =
            lastPosition < totalCount
                ? lastPosition
                : null;

        return new CardSearchResult(
            items,
            searchId,
            nextPosition,
            totalCount);
    }

    // Las columnas son internas; los valores del usuario siempre se envían como parámetros.
    private static void AddFilter(
        StringBuilder sql,
        List<NpgsqlParameter> parameters,
        string? value,
        string column,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        sql.Append($" AND {column} = @{parameterName}");

        parameters.Add(new NpgsqlParameter(
            parameterName,
            value.Trim()));
    }

    private static void AddNameFilter(
        StringBuilder sql,
        List<NpgsqlParameter> parameters,
        string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        // Escapa los comodines de LIKE para que el
        // nombre ingresado se interprete literalmente.
        var normalized = name.Trim()
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

        sql.Append("""
             AND unaccent(c.name) ILIKE
                 '%' || unaccent(@name) || '%'
                 ESCAPE '\'
            """);

        parameters.Add(new NpgsqlParameter(
            "name", normalized));
    }

    private static void AddNumberFilter(
        StringBuilder sql,
        List<NpgsqlParameter> parameters,
        int? value,
        string column,
        string name)
    {
        if (!value.HasValue)
            return;

        sql.Append($" AND {column} = @{name}");

        parameters.Add(new NpgsqlParameter(
            name, value.Value));
    }

    private static void AddMinFilter(
        StringBuilder sql,
        List<NpgsqlParameter> parameters,
        int? value,
        string column,
        string name)
    {
        if (!value.HasValue)
            return;

        sql.Append($" AND {column} >= @{name}");

        parameters.Add(new NpgsqlParameter(
            name, value.Value));
    }

    private static void AddMaxFilter(
        StringBuilder sql,
        List<NpgsqlParameter> parameters,
        int? value,
        string column,
        string name)
    {
        if (!value.HasValue)
            return;

        sql.Append($" AND {column} <= @{name}");

        parameters.Add(new NpgsqlParameter(
            name, value.Value));
    }
}
