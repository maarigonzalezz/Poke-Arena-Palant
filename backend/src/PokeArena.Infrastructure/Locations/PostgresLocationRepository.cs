using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PokeArena.Application.Common;
using PokeArena.Application.Locations;
using PokeArena.Domain.Entities;
using PokeArena.Infrastructure.Persistence;

namespace PokeArena.Infrastructure.Locations;

/// <summary>Guarda ubicación e historial con el DbContext de la operación en curso.</summary>
internal sealed class PostgresLocationRepository(PokeArenaDbContext db) : ILocationRepository
{
    public async Task<Location> CreateAsync(CreateLocationRequest request, StoredImage image,
        CancellationToken cancellationToken)
    {
        // Application ya validó los datos y generó ambas imágenes.
        var location = new Location
        {
            Name = request.Name.Trim(), Description = request.Description.Trim(), MaxCards = request.MaxCards,
            EnergyMode = request.EnergyMode, ExtraEnergy = request.ExtraEnergy, EffectType = request.EffectType,
            EffectParameters = request.EffectParameters, ImageStatus = "Ready",
            ImageOriginalPath = image.OriginalUrl, ImageSmallPath = image.SmallUrl, CurrentVersion = 1, Active = true
        };
        db.Locations.Add(location);
        await db.SaveChangesAsync(cancellationToken);
        // La identidad generada permite enlazar la instantánea sin escribir INSERT manualmente.
        db.LocationVersions.Add(new LocationVersion
        {
            LocationId = location.Id, Version = 1,
            Snapshot = JsonSerializer.Serialize(location, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        });
        await db.SaveChangesAsync(cancellationToken);
        return location;
    }
}
