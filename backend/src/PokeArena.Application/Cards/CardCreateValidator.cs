namespace PokeArena.Application.Cards;

/// <summary>Valida datos de creación antes de consultar catálogos y guardar imágenes.</summary>
public static class CardCreateValidator
{
    public static readonly IReadOnlyDictionary<string, string> TypePrefixes = new Dictionary<string, string>
    {
        ["Fuego"] = "FUE", ["Agua"] = "AGU", ["Planta"] = "PLA",
        ["Eléctrico"] = "ELE", ["Psíquico"] = "PSI", ["Lucha"] = "LUC"
    };

    public static Dictionary<string, string> Validate(CardCreateQuery query)
    {
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(query.Name) || query.Name.Trim().Length > 120)
            errors["name"] = "El nombre debe tener entre 1 y 120 caracteres.";
        if (query.Cost is < 0 or > 10) errors["cost"] = "El costo debe estar entre 0 y 10.";
        if (query.Power is < -20 or > 20) errors["power"] = "El poder debe estar entre -20 y 20.";
        if (query.Type is null || !TypePrefixes.ContainsKey(query.Type)) errors["type"] = "Tipo no permitido.";
        if (query.Rarity is not ("Común" or "Poco común" or "Rara" or "Legendaria"))
            errors["rarity"] = "Rareza no permitida.";
        if (string.IsNullOrWhiteSpace(query.AbilityCode) || query.AbilityCode.Trim().Length > 20)
            errors["abilityCode"] = "Seleccione una habilidad válida.";
        return errors;
    }
}
