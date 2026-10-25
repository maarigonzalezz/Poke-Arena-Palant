using System.Text.Json;
using Npgsql;
using PokeArena.Application.Cards;
using PokeArena.Application.Common;

namespace PokeArena.Infrastructure.Cards;

/// <summary>Crea código, imágenes, carta y versión inicial sin duplicar reintentos.</summary>
public sealed class PostgresCardCreateRepository(CatalogTransaction transactions, IImageStore images)
    : ICardCreateRepository
{
    public async Task<CardCreateResult> CreateAsync(string actorId, CardCreateQuery query, ImageUpload image,
        string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var errors = CardCreateValidator.Validate(query);
        if (errors.Count > 0) throw new CatalogValidationException(errors);
        query = query with { Name = query.Name.Trim(), AbilityCode = query.AbilityCode.Trim() };
        StoredImage? stored = null;
        try
        {
            return await transactions.RunAsync(actorId, idempotencyKey, "create-card",
                CatalogTransaction.Fingerprint(query, image), async (connection, transaction) =>
            {
                string abilityName, parameters;
                string? trigger, effect;
                await using (var ability = new NpgsqlCommand("""
                    SELECT name, trigger_type, effect_type, parameters::text, restricted_card_type
                    FROM abilities WHERE code=@code AND active=TRUE FOR SHARE
                    """, connection, transaction))
                {
                    ability.Parameters.AddWithValue("code", query.AbilityCode);
                    await using var reader = await ability.ExecuteReaderAsync(cancellationToken);
                    if (!await reader.ReadAsync(cancellationToken))
                        throw new CatalogValidationException(new() { ["abilityCode"] = "La habilidad no existe o está inactiva." });
                    if (!reader.IsDBNull(4) && reader.GetString(4) != query.Type)
                        throw new CatalogValidationException(new() { ["abilityCode"] = "La habilidad no corresponde al tipo de carta." });
                    abilityName = reader.GetString(0);
                    trigger = reader.IsDBNull(1) ? null : reader.GetString(1);
                    effect = reader.IsDBNull(2) ? null : reader.GetString(2);
                    parameters = reader.GetString(3);
                }

                // UPDATE bloquea el contador por tipo; la transacción evita códigos repetidos.
                int number;
                await using (var sequence = new NpgsqlCommand("""
                    UPDATE card_code_sequences SET last_number=GREATEST(last_number,
                        COALESCE((SELECT MAX(RIGHT(code,3)::integer) FROM cards
                          WHERE card_type=@type AND code ~ '^PKA-(FUE|AGU|PLA|ELE|PSI|LUC)-[0-9]{3}$'),0))+1
                    WHERE card_type=@type AND last_number < 999 RETURNING last_number
                    """, connection, transaction))
                {
                    sequence.Parameters.AddWithValue("type", query.Type);
                    var value = await sequence.ExecuteScalarAsync(cancellationToken);
                    if (value is null) throw new CatalogConflictException("Se agotaron los códigos de tres dígitos para este tipo.");
                    number = (int)value;
                    if (number > 999) throw new CatalogConflictException("Se agotaron los códigos de tres dígitos para este tipo.");
                }
                var code = $"PKA-{CardCreateValidator.TypePrefixes[query.Type]}-{number:D3}";
                stored = await images.SaveAsync(image, "cards", cancellationToken);
                long id;
                await using (var insert = new NpgsqlCommand("""
                    INSERT INTO cards(code,name,cost,power,card_type,rarity,ability_code,
                        image_status,image_original_path,image_small_path,current_version,active)
                    VALUES (@code,@name,@cost,@power,@type,@rarity,@ability,'Ready',@original,@small,1,TRUE)
                    RETURNING id
                    """, connection, transaction))
                {
                    insert.Parameters.AddWithValue("code", code);
                    insert.Parameters.AddWithValue("name", query.Name);
                    insert.Parameters.AddWithValue("cost", query.Cost);
                    insert.Parameters.AddWithValue("power", query.Power);
                    insert.Parameters.AddWithValue("type", query.Type);
                    insert.Parameters.AddWithValue("rarity", query.Rarity);
                    insert.Parameters.AddWithValue("ability", query.AbilityCode);
                    insert.Parameters.AddWithValue("original", stored.OriginalUrl);
                    insert.Parameters.AddWithValue("small", stored.SmallUrl);
                    id = (long)(await insert.ExecuteScalarAsync(cancellationToken))!;
                }
                var item = new CardCreateItem(id, code, query.Name, query.Cost, query.Power,
                    query.Type, query.Rarity, "Ready", stored.OriginalUrl, query.AbilityCode,
                    abilityName, trigger, effect, parameters, stored.SmallUrl);
                await using var version = new NpgsqlCommand("""
                    INSERT INTO card_versions(card_id,version,snapshot) VALUES (@id,1,@snapshot::jsonb)
                    """, connection, transaction);
                version.Parameters.AddWithValue("id", id);
                version.Parameters.AddWithValue("snapshot", JsonSerializer.Serialize(item, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                await version.ExecuteNonQueryAsync(cancellationToken);
                return new CardCreateResult(item);
            }, cancellationToken);
        }
        catch { if (stored is not null) images.Delete(stored); throw; }
    }
}
