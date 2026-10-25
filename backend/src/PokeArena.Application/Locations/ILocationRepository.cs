using PokeArena.Application.Common;
using PokeArena.Domain.Entities;

namespace PokeArena.Application.Locations;

public interface ILocationRepository
{
    Task<Location> CreateAsync(string actorId, CreateLocationRequest request, ImageUpload image,
        string idempotencyKey, CancellationToken cancellationToken);
}
