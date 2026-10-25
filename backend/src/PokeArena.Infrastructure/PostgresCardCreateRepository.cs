
using Npgsql;
using PokeArena.Application.Cards;

namespace PokeArena.Infrastructure.Cards;

/// <summary>Crear nuevas entradas del repositorio.</summary>
public sealed class PostgresCardCreateRepository
    : ICardCreateRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _cardsDirectory;

    public PostgresCardCreateRepository(
        NpgsqlDataSource dataSource,
        string cardsDirectory)
    {
        _dataSource = dataSource;
        _cardsDirectory = cardsDirectory;
    }

    public async Task<CardCreateResult> CreateAsync(
        string actorId,
        CardCreateQuery query,
        CardFileImage image,
        Stream imageContent,
        CancellationToken cancellationToken = default
    )
    {
        var extension = image.Format.ToUpperInvariant() switch
        {
            "JPG" or "JPEG" => ".jpg",
            "PNG" => ".png",
            "WEBP" => ".webp",
            _ => throw new ArgumentException(
                "Unsupported card image format.",
                nameof(image))
        };
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(_cardsDirectory, fileName);
        var imageUrl = $"/files/cards/{fileName}";
        var imageSaved = false;

        try
        {
            Directory.CreateDirectory(_cardsDirectory);

            await using (var destination = new FileStream(
                physicalPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await imageContent.CopyToAsync(
                    destination,
                    cancellationToken);
            }

            await using var connection =
                await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction =
                await connection.BeginTransactionAsync(cancellationToken);

            const string sql = """
                WITH created AS (
                    INSERT INTO cards (
                        code,
                        name,
                        cost,
                        power,
                        card_type,
                        rarity,
                        ability_code,
                        current_version,
                        image_status,
                        image_original_path,
                        image_small_path
                    )
                    VALUES (
                        @code,
                        @name,
                        @cost,
                        @power,
                        @type,
                        @rarity,
                        @abilityCode,
                        @version,
                        'Ready',
                        @imageOriginalPath,
                        NULL
                    )
                    RETURNING
                        id,
                        code,
                        name,
                        cost,
                        power,
                        card_type,
                        rarity,
                        ability_code,
                        image_status,
                        image_original_path
                )
                SELECT
                    c.id,
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
                FROM created c
                INNER JOIN abilities a
                    ON a.code = c.ability_code
                """;

            await using var command = new NpgsqlCommand(
                sql,
                connection,
                transaction);
            command.Parameters.AddWithValue("code", query.Code);
            command.Parameters.AddWithValue("name", query.Name);
            command.Parameters.AddWithValue("cost", query.Cost);
            command.Parameters.AddWithValue("power", query.Power);
            command.Parameters.AddWithValue("type", query.Type);
            command.Parameters.AddWithValue("rarity", query.Rarity);
            command.Parameters.AddWithValue("abilityCode", query.AbilityCode);
            command.Parameters.AddWithValue("version", query.Version);
            command.Parameters.AddWithValue("imageOriginalPath", imageUrl);

            CardCreateItem item;
            await using (var reader =
                await command.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException(
                        "The card insert completed without returning the created card.");
                }

                item = new CardCreateItem(
                    Id: reader.GetInt64(0),
                    Code: reader.GetString(1),
                    Name: reader.GetString(2),
                    Cost: reader.GetInt32(3),
                    Power: reader.GetInt32(4),
                    Type: reader.GetString(5),
                    Rarity: reader.GetString(6),
                    ImageStatus: reader.GetString(7),
                    ImageUrl: reader.IsDBNull(8) ? null : reader.GetString(8),
                    AbilityCode: reader.GetString(9),
                    AbilityName: reader.GetString(10),
                    AbilityTrigger: reader.IsDBNull(11)
                        ? null
                        : reader.GetString(11),
                    AbilityEffect: reader.IsDBNull(12)
                        ? null
                        : reader.GetString(12),
                    AbilityParameters: reader.GetString(13)
                );
            }

            await transaction.CommitAsync(cancellationToken);
            imageSaved = true;

            return new CardCreateResult(item);
        }
        finally
        {
            if (!imageSaved && File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }
        }
    }
}