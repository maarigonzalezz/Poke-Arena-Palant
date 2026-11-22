using PokeArena.Domain.Catalogs;

namespace PokeArena.Application.Cards;

/// <summary>Valida datos de creación antes de consultar catálogos y guardar imágenes.</summary>
public static class CardCreateValidator
{
    public static Dictionary<string, string> Validate(CardCreateQuery query)
    {
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(query.Name) || query.Name.Trim().Length > GameRules.MaximumNameLength)
            errors["name"] = "El nombre debe tener entre 1 y 120 caracteres.";
        if (query.Cost is < GameRules.MinimumCost or > GameRules.MaximumCost) errors["cost"] = "El costo debe estar entre 0 y 10.";
        if (query.Power is < GameRules.MinimumPower or > GameRules.MaximumPower) errors["power"] = "El poder debe estar entre -20 y 20.";
        if (!CardTypes.Contains(query.Type)) errors["type"] = "Tipo no permitido.";
        if (query.Rarity is not ("Común" or "Poco común" or "Rara" or "Legendaria"))
            errors["rarity"] = "Rareza no permitida.";
        // La validación de habilidad completa se realiza en CatalogValidationService.
        if (string.IsNullOrWhiteSpace(query.AbilityCode) || query.AbilityCode.Trim().Length > 20)
            errors["abilityCode"] = "Seleccione una habilidad válida.";
        return errors;
    }
}
