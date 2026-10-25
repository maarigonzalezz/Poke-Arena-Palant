using System.Text.Json;
using Npgsql;
using PokeArena.Application.Common;
using PokeArena.Application.Locations;
using PokeArena.Domain.Entities;

namespace PokeArena.Infrastructure.Locations;

/// <summary>Persiste la ubicación, sus imágenes y su versión inicial en una operación repetible.</summary>
public sealed class PostgresLocationRepository(CatalogTransaction transactions, IImageStore images) : ILocationRepository
{
    public async Task<Location> CreateAsync(string actorId, CreateLocationRequest request, ImageUpload image,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        var errors = LocationValidator.Validate(request);
        if (errors.Count > 0) throw new CatalogValidationException(new() { ["location"] = string.Join(" ", errors) });
        StoredImage? stored = null;
        try
        {
            return await transactions.RunAsync(actorId, idempotencyKey, "create-location",
                CatalogTransaction.Fingerprint(request, image), async (connection, transaction) =>
            {
                stored = await images.SaveAsync(image, "locations", cancellationToken);
                await using var insert = new NpgsqlCommand("""
                    INSERT INTO locations(name,description,max_cards,energy_mode,extra_energy,effect_type,
                        effect_parameters,image_status,image_original_path,image_small_path,current_version,active)
                    VALUES (@name,@description,@max,@mode,@energy,@effect,@parameters::jsonb,'Ready',@original,@small,1,TRUE)
                    RETURNING id
                    """, connection, transaction);
                insert.Parameters.AddWithValue("name", request.Name.Trim());
                insert.Parameters.AddWithValue("description", request.Description.Trim());
                insert.Parameters.AddWithValue("max", request.MaxCards);
                insert.Parameters.AddWithValue("mode", request.EnergyMode);
                insert.Parameters.AddWithValue("energy", request.ExtraEnergy);
                insert.Parameters.AddWithValue("effect", request.EffectType);
                insert.Parameters.AddWithValue("parameters", request.EffectParameters);
                insert.Parameters.AddWithValue("original", stored.OriginalUrl);
                insert.Parameters.AddWithValue("small", stored.SmallUrl);
                var id = (long)(await insert.ExecuteScalarAsync(cancellationToken))!;
                var created = new Location
                {
                    Id=id, Name=request.Name.Trim(), Description=request.Description.Trim(),
                    MaxCards=request.MaxCards, EnergyMode=request.EnergyMode, ExtraEnergy=request.ExtraEnergy,
                    EffectType=request.EffectType, EffectParameters=request.EffectParameters,
                    ImageStatus="Ready", ImageOriginalPath=stored.OriginalUrl, ImageSmallPath=stored.SmallUrl,
                    CurrentVersion=1, Active=true
                };
                await using var version = new NpgsqlCommand("""
                    INSERT INTO location_versions(location_id,version,snapshot) VALUES (@id,1,@snapshot::jsonb)
                    """, connection, transaction);
                version.Parameters.AddWithValue("id", id);
                version.Parameters.AddWithValue("snapshot", JsonSerializer.Serialize(created, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                await version.ExecuteNonQueryAsync(cancellationToken);
                return created;
            }, cancellationToken);
        }
        catch { if (stored is not null) images.Delete(stored); throw; }
    }
}
