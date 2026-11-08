
namespace PokeArena.Application.Inventory;

public sealed class InventoryService(
    InventoryValidationService validation, 
    IInventoryRepository repository
) {
    public Task<PlayerProfileResult> ConsultAsync(
        PlayerProfileQuery query, 
        CancellationToken cancellationToken
    ){
        validation.ValidateProfile(query);
        return repository.ConsultAsync(query, cancellationToken);
    }
}