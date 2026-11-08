
namespace PokeArena.Application.Inventory;

public sealed class InventoryValidationService
{
    public void ValidateProfile (PlayerProfileQuery query)
    {
        var errors = PlayerProfileValidator.Validate(query.Profile);
        if (errors.Count > 0)
            throw new InventoryValidationException(errors, "invalid_profile", "Las datos del perfil son inválidos");
    }
}