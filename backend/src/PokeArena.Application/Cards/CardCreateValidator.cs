
namespace PokeArena.Application.Cards;

/// <summary>Comprueba límites, catálogos permitidos y coherencia de los filtros.</summary>
public static class CardCreateValidator
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

    private static readonly HashSet<string> Formats =
    [
        "JPG",
        "PNG",
        "WebP"
    ];

    public static Dictionary<string, string> Validate(
        CardCreateQuery query,
        CardFileImage image
    )
    {
        var errors = new Dictionary<string, string>();

        // << Revisar las propiedades del query >>
        string trim_code = query.Code.Trim();
        if (trim_code.Length == 0 && trim_code.Length > 20)
        {
            errors["code"] = "Codigo debe tener entre 1 y 20 caracteres validos";
        }

        string trim_name = query.Name.Trim();
        if (trim_name.Length == 0 && trim_name.Length > 120)
        {
            errors["name"] = "Nombre debe tener entre 1 y 120 caracteres validos";
        }

        if (query.Cost < 0 || query.Cost > 10)
        {
            errors["cost"] = "Costo debe estar entre 0 y 10";
        }

        if (query.Power < -20 || query.Power > 20)
        {
            errors["power"] = "Poder debe estar entre -20 y 20";
        }

        if (!Types.Contains(query.Type))
        {
            errors["type"] = "Tipo no permitdo";
        }

        if (!Rarities.Contains(query.Rarity))
        {
            errors["rarity"] = "Rareza no permitida";
        }

        string trim_ability = query.AbilityCode.Trim();
        if (trim_ability.Length == 0 || trim_ability.Length > 20)
        {
            errors["ability"] = "Codigo de habilidad debe tener entre 1 y 20 caracteres validos";
        }

        if (query.Version <= 0)
        {
            errors["version"] = "La version debe ser mayor a 1";
        }

        // << Revisar las propiedades de la imagen >>
        string trim_filename = image.Name.Trim();
        if (trim_filename.Length == 0)
        {
            errors["image.name"] = "La imagen debe tener un nombre";
        }

        if (!Formats.Contains(image.Format))
        {
            errors["image.format"] = "La imagen debe tener un formato JPG, PNG o WebP";
        }

        if (image.hProportion != 5 || image.vProportion != 7)
        {
            errors["image.proportions"] = "La imagen debe tener una proprocion vertical 5:7";
        }

        if (image.hPixels < 600 || image.vPixels < 840)
        {
            errors["image.resolution"] = "La imagen debe tener una resolucion minima de 600 x 840";
        }

        return errors;
    }
}