
using PokeArena.Domain.Entities;

namespace PokeArena.Application.Locations;

/// <summary>
/// Define las operaciones de persistencia para las ubicaciones.
/// </summary>
public interface ILocationRepository
{
    /// <summary>
    /// Guarda una ubicación y registra su versión inicial.
    /// </summary>
    /// <param name="location">Ubicación que se desea guardar.</param>
    /// <param name="cancellationToken">
    /// Token para cancelar la operación.
    /// </param>
    /// <returns>La ubicación guardada con su identificador asignado.</returns>
    Task<Location> CreateAsync(
        Location location,
        CancellationToken cancellationToken);
}
