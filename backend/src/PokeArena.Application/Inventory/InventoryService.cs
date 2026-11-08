
namespace PokeArena.Application.Inventory;

public sealed class InventoryService(
    InventoryValidationService validation, 
    IInventoryRepository repository
) {
    /// <summary> Consultar el inventario de un jugador</summary>
    public Task<PlayerProfileResult> ConsultAsync(
        Guid playerId,
        CancellationToken cancellationToken
    ){
        validation.ValidatePlayer(playerId);
        return repository.ConsultAsync(playerId, cancellationToken);
    }

    // Cada ingreso puede reintentar; el repositorio reconoce entregas ya completadas.
    public Task<InitialCollectionResult> EnsureInitialCollectionAsync(Guid playerId, CancellationToken cancellationToken)
    {
        validation.ValidatePlayer(playerId);
        return repository.EnsureInitialCollectionAsync(playerId, cancellationToken);
    }

    public Task<CubeHistoryResult> GetCubeHistoryAsync(Guid playerId, int after, CancellationToken cancellationToken)
    {
        validation.ValidatePlayer(playerId);
        if (after < 0) throw new InventoryValidationException(new() { ["after"] = "Debe ser cero o positivo." });
        return repository.GetCubeHistoryAsync(playerId, after, cancellationToken);
    }
}
