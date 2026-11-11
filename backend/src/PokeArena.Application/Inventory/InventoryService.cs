
namespace PokeArena.Application.Inventory;

// Expone las operaciones; el repositorio comprueba las condiciones sobre los datos persistidos.
public sealed class InventoryService(IInventoryRepository repository) {
    /// <summary>Consulta el inventario sin entregar cartas ni acreditar cubos.</summary>
    public Task<PlayerProfileResult> ConsultAsync(
        Guid playerId,
        CancellationToken cancellationToken
    ){
        return repository.ConsultAsync(playerId, cancellationToken);
    }

    // Cada ingreso puede reintentar; el repositorio reconoce entregas ya completadas.
    public Task<InitialCollectionResult> EnsureInitialCollectionAsync(Guid playerId, CancellationToken cancellationToken)
    {
        return repository.EnsureInitialCollectionAsync(playerId, cancellationToken);
    }

    public Task<CubeHistoryResult> GetCubeHistoryAsync(Guid playerId, int after, CancellationToken cancellationToken)
    {
        return repository.GetCubeHistoryAsync(playerId, after, cancellationToken);
    }
}
