
namespace PokeArena.Application.Cards;

/// <summary>Comprueba límites, catálogos permitidos y coherencia de los filtros y la paginación.</summary>
public static class CardSearchValidator
{
    private static readonly HashSet<string> Types =
    [
        "Fuego",
        "Agua",
        "Planta",
        "Eléctrico",
        "Psíquico",
        "Lucha"
    ];

    private static readonly HashSet<string> Rarities =
    [
        "Común",
        "Poco común",
        "Rara",
        "Legendaria"
    ];

    /// <summary>Devuelve los errores por campo; un diccionario vacío indica criterios válidos.</summary>
    public static Dictionary<string, string> Validate(
        CardSearchQuery query)
    {
        var errors = new Dictionary<string, string>();

        if (query.PageSize < 1 || query.PageSize > 100)
        {
            errors["pageSize"] =
                "Debe estar entre 1 y 100.";
        }

        if (query.After < 0)
        {
            errors["after"] =
                "No puede ser negativo.";
        }

        if (query.SearchId is null && query.After != 0)
        {
            errors["after"] =
                "Se requiere searchId para continuar.";
        }

        if (query.Cost is < 0 or > 10)
            errors["cost"] = "Debe estar entre 0 y 10.";

        if (query.MinCost is < 0 or > 10)
            errors["minCost"] = "Debe estar entre 0 y 10.";

        if (query.MaxCost is < 0 or > 10)
            errors["maxCost"] = "Debe estar entre 0 y 10.";

        if (query.Power is < -20 or > 20)
            errors["power"] = "Debe estar entre -20 y 20.";

        if (query.MinPower is < -20 or > 20)
            errors["minPower"] =
                "Debe estar entre -20 y 20.";

        if (query.MaxPower is < -20 or > 20)
            errors["maxPower"] =
                "Debe estar entre -20 y 20.";

        if (query.MinCost > query.MaxCost)
            errors["costRange"] =
                "El mínimo supera al máximo.";

        if (query.MinPower > query.MaxPower)
            errors["powerRange"] =
                "El mínimo supera al máximo.";

        if (query.Cost.HasValue &&
            (query.MinCost.HasValue ||
             query.MaxCost.HasValue))
        {
            errors["cost"] =
                "Use un valor exacto o un rango.";
        }

        if (query.Power.HasValue &&
            (query.MinPower.HasValue ||
             query.MaxPower.HasValue))
        {
            errors["power"] =
                "Use un valor exacto o un rango.";
        }

        if (query.Type is not null &&
            !Types.Contains(query.Type))
        {
            errors["type"] = "Tipo no permitido.";
        }

        if (query.Rarity is not null &&
            !Rarities.Contains(query.Rarity))
        {
            errors["rarity"] = "Rareza no permitida.";
        }

        if (query.SearchId.HasValue)
        {
            if (query.Code is not null ||
                query.Name is not null ||
                query.Cost.HasValue ||
                query.MinCost.HasValue ||
                query.MaxCost.HasValue ||
                query.Power.HasValue ||
                query.MinPower.HasValue ||
                query.MaxPower.HasValue ||
                query.Type is not null ||
                query.Rarity is not null ||
                query.AbilityCode is not null ||
                query.AbilityTrigger is not null ||
                query.AbilityEffect is not null)
            {
                errors["searchId"] =
                    "No envíe filtros al continuar una búsqueda.";
            }
        }

        return errors;
    }
}
