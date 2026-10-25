using System.Text.Json;
using Npgsql;
using PokeArena.Application.Locations;
using PokeArena.Domain.Entities;

namespace PokeArena.Infrastructure.Locations;

/// <summary>
/// Implementa la persistencia de ubicaciones y sus versiones en PostgreSQL.
/// </summary>
/// <remarks>
/// La ubicación y su instantánea inicial se insertan en una misma transacción
/// para evitar guardar solo una de las dos filas.
/// </remarks>
public sealed class PostgresLocationRepository : ILocationRepository
{
    private readonly NpgsqlDataSource _dataSource;

    /// <summary>
    /// Inicializa el repositorio con el origen de conexiones de PostgreSQL.
    /// </summary>
    /// <param name="dataSource">Origen compartido para abrir conexiones.</param>
    public PostgresLocationRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <summary>
    /// Inserta una ubicación y su instantánea de versión inicial.
    /// </summary>
    /// <param name="location">Datos de la ubicación que se guardará.</param>
    /// <param name="cancellationToken">Token para cancelar las operaciones de base de datos.</param>
    /// <returns>La ubicación creada con los valores generados por PostgreSQL.</returns>
    public async Task<Location> CreateAsync(
        Location location,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        const string insertLocationSql = """
            INSERT INTO locations (
                name,
                description,
                max_cards,
                energy_mode,
                extra_energy,
                effect_type,
                effect_parameters
            )
            VALUES (
                @name,
                @description,
                @maxCards,
                @energyMode,
                @extraEnergy,
                @effectType,
                @effectParameters::jsonb
            )
            RETURNING
                id,
                name,
                description,
                max_cards,
                energy_mode,
                extra_energy,
                effect_type,
                effect_parameters::text,
                image_status,
                image_original_path,
                image_small_path,
                current_version,
                active
            """;

        Location created;
        await using (var command =
            new NpgsqlCommand(insertLocationSql, connection, transaction))
        {
            command.Parameters.AddWithValue("name", location.Name);
            command.Parameters.AddWithValue("description", location.Description);
            command.Parameters.AddWithValue("maxCards", location.MaxCards);
            command.Parameters.AddWithValue("energyMode", location.EnergyMode);
            command.Parameters.AddWithValue("extraEnergy", location.ExtraEnergy);
            command.Parameters.AddWithValue("effectType", location.EffectType);
            command.Parameters.AddWithValue(
                "effectParameters",
                location.EffectParameters);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);

            created = new Location
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                MaxCards = reader.GetInt32(3),
                EnergyMode = reader.GetString(4),
                ExtraEnergy = reader.GetInt32(5),
                EffectType = reader.GetString(6),
                EffectParameters = reader.GetString(7),
                ImageStatus = reader.GetString(8),
                ImageOriginalPath = reader.IsDBNull(9)
                    ? null
                    : reader.GetString(9),
                ImageSmallPath = reader.IsDBNull(10)
                    ? null
                    : reader.GetString(10),
                CurrentVersion = reader.GetInt32(11),
                Active = reader.GetBoolean(12)
            };
        }

        const string insertVersionSql = """
            INSERT INTO location_versions (
                location_id,
                version,
                snapshot
            )
            VALUES (
                @locationId,
                @version,
                @snapshot::jsonb
            )
            """;

        await using (var command =
            new NpgsqlCommand(insertVersionSql, connection, transaction))
        {
            command.Parameters.AddWithValue("locationId", created.Id);
            command.Parameters.AddWithValue("version", created.CurrentVersion);
            command.Parameters.AddWithValue(
                "snapshot",
                JsonSerializer.Serialize(created));

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return created;
    }
}
