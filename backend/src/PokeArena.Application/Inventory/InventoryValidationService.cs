using PokeArena.Application.Authentication;

namespace PokeArena.Application.Inventory;

public static class PlayerProfileValidator
{
    public static Dictionary<string, string> Validate(
        PlayerProfile profile
    ){
        var errors = new Dictionary<string, string>();

        if (profile.DisplayName is null)
        {
            errors["displayName"] = "El nombre del jugador no puede ser nulo";
        }

        if (profile.Email is null)
        {
            errors["Email"] = "El correo del jugador no puede ser nulo";
        }

        if(profile.Role != "Player")
        {
            errors["Role"] = "Esta cuenta no tiene rol de jugador";
        }

        if (profile.Elo < 0)
        {
            errors["Elo"] = "El jugador no puede tener ELO negativo";
        }

        if (profile.Cubes < 0)
        {
            errors["Cubes"] = "El jugador no puede tener menos de 0 Cubos en su inventario";
        }

        return errors;
    }
}

public sealed class InventoryValidationService
{   
    /// <summary>Verificar los campos del perfil de un jugador.</summary>
    public void ValidateProfile (PlayerProfileQuery query)
    {
        var errors = PlayerProfileValidator.Validate(query.Profile);        
        if (errors.Count > 0)
            throw new InventoryValidationException(errors, "invalid_profile", "Las datos del perfil son inválidos");
    }
}