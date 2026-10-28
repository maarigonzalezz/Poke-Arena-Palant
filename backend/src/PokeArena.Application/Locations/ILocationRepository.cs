using PokeArena.Application.Common;
using PokeArena.Domain.Entities;

namespace PokeArena.Application.Locations;

// Separa la creación de ubicaciones de los detalles de SQL y almacenamiento.
public interface ILocationRepository
{
    Task<Location> CreateAsync(string actorId, CreateLocationRequest request, ImageUpload image,
        string idempotencyKey, CancellationToken cancellationToken);
}
