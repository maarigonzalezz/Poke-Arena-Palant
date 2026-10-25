
namespace PokeArena.Application.Locations;

/// <summary>
/// Valida los datos de entrada para crear una ubicación.
/// </summary>
/// <remarks>
/// Comprueba los campos generales y que los parámetros JSON coincidan con el
/// efecto seleccionado. Devuelve todos los errores detectados en una sola
/// ejecución para que la API pueda informar al cliente de forma conjunta.
/// </remarks>
public static class LocationValidator
{
    /// <summary>Tipos de carta permitidos en parámetros de efectos.</summary>
    private static readonly string[] AllowedCardTypes =
    [
        "Fuego",
        "Agua",
        "Planta",
        "Eléctrico",
        "Psíquico",
        "Lucha"
    ];

    /// <summary>
    /// Valida los campos y las restricciones de una ubicación.
    /// </summary>
    /// <param name="request">Datos de la ubicación que se desea crear.</param>
    /// <returns>Lista de errores encontrados; vacía si la solicitud es válida.</returns>
    public static List<string> Validate(
        CreateLocationRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add("El nombre es obligatorio.");
        }
        else if (request.Name.Length > 120)
        {
            errors.Add(
                "El nombre no puede superar los 120 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            errors.Add("La descripción es obligatoria.");
        }

        if (request.MaxCards < 0 || request.MaxCards > 5)
        {
            errors.Add(
                "El límite de cartas debe estar entre 0 y 5.");
        }

        if (request.EnergyMode is not "Fixed" and not "PerCard")
        {
            errors.Add(
                "El modo de energía debe ser Fixed o PerCard.");
        }
        else if (request.EnergyMode == "Fixed"
            && (request.ExtraEnergy < 0 || request.ExtraEnergy > 3))
        {
            errors.Add(
                "La energía adicional debe estar entre 0 y 3 para el modo Fixed.");
        }
        else if (request.EnergyMode == "PerCard"
            && (request.ExtraEnergy < 0 || request.ExtraEnergy > 2))
        {
            errors.Add(
                "La energía adicional debe estar entre 0 y 2 para el modo PerCard.");
        }

        string[] allowedEffectTypes =
        [
            "None",
            "TypeBonus",
            "CostModifier",
            "TurnRestriction"
        ];

        if (!allowedEffectTypes.Contains(request.EffectType))
        {
            errors.Add("El tipo de efecto seleccionado no es válido.");
        }

        if (string.IsNullOrWhiteSpace(request.EffectParameters))
        {
            errors.Add("Los parámetros del efecto son obligatorios.");
        }
        else
        {
            try
            {
                using var document =
                    System.Text.Json.JsonDocument.Parse(
                        request.EffectParameters);

                if (document.RootElement.ValueKind
                    != System.Text.Json.JsonValueKind.Object)
                {
                    errors.Add(
                        "Los parámetros del efecto deben ser un objeto JSON.");
                }
                else
                {
                    ValidateEffectParameters(
                        request.EffectType,
                        document.RootElement,
                        errors);
                }
            }
            catch (System.Text.Json.JsonException)
            {
                errors.Add(
                    "Los parámetros del efecto contienen JSON inválido.");
            }
        }

        return errors;
    }

    /// <summary>
    /// Valida las propiedades requeridas por el tipo de efecto seleccionado.
    /// </summary>
    /// <param name="effectType">Tipo de efecto solicitado.</param>
    /// <param name="parameters">Objeto JSON con los parámetros del efecto.</param>
    /// <param name="errors">Lista donde se agregan los errores encontrados.</param>
    private static void ValidateEffectParameters(
        string effectType,
        System.Text.Json.JsonElement parameters,
        List<string> errors)
    {
        switch (effectType)
        {
            case "TypeBonus":
                if (!TryGetAllowedCardType(parameters, "type"))
                {
                    errors.Add(
                        "El efecto TypeBonus requiere un tipo de carta válido.");
                }

                if (!TryGetInteger(parameters, "power", -5, 5))
                {
                    errors.Add(
                        "La bonificación de poder debe ser un entero entre -5 y 5.");
                }

                break;

            case "CostModifier":
                if (!TryGetInteger(parameters, "amount", -5, 5))
                {
                    errors.Add(
                        "El modificador de costo debe ser un entero entre -5 y 5.");
                }

                if (parameters.TryGetProperty(
                        "affectedType",
                        out var affectedType)
                    && affectedType.ValueKind != System.Text.Json.JsonValueKind.Null
                    && (affectedType.ValueKind
                            != System.Text.Json.JsonValueKind.String
                        || !AllowedCardTypes.Contains(
                            affectedType.GetString())))
                {
                    errors.Add(
                        "El tipo afectado debe ser un tipo de carta válido o null.");
                }

                break;

            case "TurnRestriction":
                if (!parameters.TryGetProperty("turns", out var turns)
                    || turns.ValueKind
                        != System.Text.Json.JsonValueKind.Array
                    || turns.GetArrayLength() == 0)
                {
                    errors.Add(
                        "El efecto TurnRestriction requiere al menos un turno.");
                    break;
                }

                var selectedTurns = new HashSet<int>();

                foreach (var turn in turns.EnumerateArray())
                {
                    if (turn.ValueKind
                            != System.Text.Json.JsonValueKind.Number
                        || !turn.TryGetInt32(out var turnNumber)
                        || turnNumber < 1
                        || turnNumber > 6
                        || !selectedTurns.Add(turnNumber))
                    {
                        errors.Add(
                            "Los turnos restringidos deben ser enteros únicos entre 1 y 6.");
                        break;
                    }
                }

                break;
        }
    }

    /// <summary>
    /// Comprueba que una propiedad JSON contenga uno de los tipos de carta admitidos.
    /// </summary>
    /// <param name="parameters">Objeto JSON que contiene los parámetros del efecto.</param>
    /// <param name="propertyName">Nombre de la propiedad que se debe comprobar.</param>
    /// <returns>
    /// <see langword="true"/> si la propiedad existe, es texto y contiene un tipo admitido;
    /// de lo contrario, <see langword="false"/>.
    /// </returns>
    private static bool TryGetAllowedCardType(
        System.Text.Json.JsonElement parameters,
        string propertyName)
    {
        return parameters.TryGetProperty(propertyName, out var value)
            && value.ValueKind == System.Text.Json.JsonValueKind.String
            && AllowedCardTypes.Contains(value.GetString());
    }

    /// <summary>
    /// Comprueba que una propiedad JSON sea un entero dentro del rango indicado.
    /// </summary>
    /// <param name="parameters">Objeto JSON que contiene los parámetros del efecto.</param>
    /// <param name="propertyName">Nombre de la propiedad que se debe comprobar.</param>
    /// <param name="minimum">Valor mínimo permitido, inclusive.</param>
    /// <param name="maximum">Valor máximo permitido, inclusive.</param>
    /// <returns>
    /// <see langword="true"/> si la propiedad es un entero dentro del rango;
    /// de lo contrario, <see langword="false"/>.
    /// </returns>
    private static bool TryGetInteger(
        System.Text.Json.JsonElement parameters,
        string propertyName,
        int minimum,
        int maximum)
    {
        return parameters.TryGetProperty(propertyName, out var value)
            && value.ValueKind == System.Text.Json.JsonValueKind.Number
            && value.TryGetInt32(out var number)
            && number >= minimum
            && number <= maximum;
    }
}
