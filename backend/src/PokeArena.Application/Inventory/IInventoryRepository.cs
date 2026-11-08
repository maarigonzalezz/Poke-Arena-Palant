
namespace PokeArena.Application.Inventory;

public interface IInventoryRepository
{
    /// <summary>Consultar el inventario de un jugador. Si no tiene inventario, genera uno automaticamente.</summary>
    Task<PlayerProfileResult> ConsultAsync(
        PlayerProfileQuery query,
        CancellationToken cancellationToken = default
    );
}