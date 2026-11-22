using PokeArena.Application.Common;
using PokeArena.Domain.Entities;

namespace PokeArena.Application.Locations;

// Separa la creación de ubicaciones de los detalles de SQL y almacenamiento.
public interface ILocationRepository
{
    Task<Location> CreateAsync(CreateLocationRequest request, StoredImage image, CancellationToken cancellationToken);
}
