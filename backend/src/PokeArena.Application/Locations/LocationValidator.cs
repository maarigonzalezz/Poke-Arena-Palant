using PokeArena.Domain.Catalogs;

namespace PokeArena.Application.Locations;

/// <summary>Comprueba capacidad, energía y parámetros del efecto antes de guardar.</summary>
public static class LocationValidator
{
    /// <summary>Acumula errores para informar todas las reglas incumplidas en una respuesta.</summary>
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

        // Cero es una capacidad válida y significa que no se admiten cartas.
        if (request.MaxCards < 0 || request.MaxCards > 5)
        {
            errors.Add(
                "El límite de cartas debe estar entre 0 y 5.");
        }

        // Valida primero la modalidad y luego el rango de energía correspondiente.
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
                // Analiza el JSON antes de comprobar las propiedades específicas del efecto.
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

    /// <summary>Valida las propiedades requeridas por el tipo de efecto seleccionado.</summary>
    private static void ValidateEffectParameters(
        string effectType,
        System.Text.Json.JsonElement parameters,
        List<string> errors)
    {
        // Cada efecto define su propio contrato de parámetros.
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

            // affectedType nulo aplica el modificador a todos los tipos.
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
                        || !CardTypes.Contains(
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

                // El conjunto permite detectar turnos repetidos además de validar el rango de 1 a 6.
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

    /// <summary>Evita que un efecto haga referencia a un tipo de carta inexistente.</summary>
    private static bool TryGetAllowedCardType(
        System.Text.Json.JsonElement parameters,
        string propertyName)
    {
        return parameters.TryGetProperty(propertyName, out var value)
            && value.ValueKind == System.Text.Json.JsonValueKind.String
            && CardTypes.Contains(value.GetString());
    }

    /// <summary>Rechaza números fraccionarios o fuera del rango permitido para el efecto.</summary>
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
