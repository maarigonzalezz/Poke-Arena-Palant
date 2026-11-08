
namespace PokeArena.Application.Inventory;

public interface IInventoryRepository
{
    // Consultar nunca entrega premios; el identificador procede de la identidad autenticada.
    Task<PlayerProfileResult> ConsultAsync(
        Guid playerId,
        CancellationToken cancellationToken = default
    );
    Task<InitialCollectionResult> EnsureInitialCollectionAsync(Guid playerId, CancellationToken cancellationToken = default);
    Task<CubeHistoryResult> GetCubeHistoryAsync(Guid playerId, int after, CancellationToken cancellationToken = default);
}
