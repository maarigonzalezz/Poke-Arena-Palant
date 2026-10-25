using PokeArena.Application.Locations;

namespace PokeArena.Tests.Locations;

/// <summary>
/// Verifica las reglas de validación para la creación de ubicaciones.
/// </summary>
public sealed class LocationValidatorTests
{
    /// <summary>
    /// Comprueba que una solicitud válida no genere errores.
    /// </summary>
    [Fact]
    public void Validate_WithValidLocation_ReturnsNoErrors()
    {
        var request = new CreateLocationRequest
        {
            Name = "Volcán",
            Description = "Una ubicación de prueba.",
            MaxCards = 4,
            EnergyMode = "Fixed",
            ExtraEnergy = 2,
            EffectType = "TypeBonus",
            EffectParameters = """{"type":"Fuego","power":2}"""
        };

        Assert.Empty(LocationValidator.Validate(request));
    }

    /// <summary>
    /// Comprueba que los valores generales inválidos sean rechazados.
    /// </summary>
    [Theory]
    [InlineData(-1, "Fixed", 0, "None", "{}")]
    [InlineData(6, "Fixed", 0, "None", "{}")]
    [InlineData(3, "Unknown", 0, "None", "{}")]
    [InlineData(3, "Fixed", 4, "None", "{}")]
    [InlineData(3, "PerCard", 3, "None", "{}")]
    [InlineData(3, "Fixed", 0, "Unknown", "{}")]
    [InlineData(3, "Fixed", 0, "None", "[]")]
    [InlineData(3, "Fixed", 0, "None", "{invalid}")]
    public void Validate_WithInvalidValues_ReturnsErrors(
        int maxCards,
        string energyMode,
        int extraEnergy,
        string effectType,
        string effectParameters)
    {
        var request = new CreateLocationRequest
        {
            Name = "Ubicación",
            Description = "Descripción",
            MaxCards = maxCards,
            EnergyMode = energyMode,
            ExtraEnergy = extraEnergy,
            EffectType = effectType,
            EffectParameters = effectParameters
        };

        Assert.NotEmpty(LocationValidator.Validate(request));
    }

    /// <summary>
    /// Comprueba que el nombre y la descripción sean obligatorios.
    /// </summary>
    [Fact]
    public void Validate_WithMissingNameAndDescription_ReturnsRequiredErrors()
    {
        var request = new CreateLocationRequest
        {
            Name = " ",
            Description = "",
            MaxCards = 0,
            EnergyMode = "Fixed",
            ExtraEnergy = 0,
            EffectType = "None",
            EffectParameters = "{}"
        };

        var errors = LocationValidator.Validate(request);

        Assert.Contains("El nombre es obligatorio.", errors);
        Assert.Contains("La descripción es obligatoria.", errors);
    }

    /// <summary>
    /// Comprueba que los parámetros de cada tipo de efecto respeten su contrato.
    /// </summary>
    [Theory]
    [InlineData("TypeBonus", """{"type":"Otro","power":2}""")]
    [InlineData("TypeBonus", """{"type":"Fuego","power":6}""")]
    [InlineData("CostModifier", """{"amount":1.5,"affectedType":null}""")]
    [InlineData("CostModifier", """{"amount":1,"affectedType":"Otro"}""")]
    [InlineData("TurnRestriction", """{"turns":[]}""")]
    [InlineData("TurnRestriction", """{"turns":[1,1]}""")]
    [InlineData("TurnRestriction", """{"turns":[7]}""")]
    public void Validate_WithInvalidEffectParameters_ReturnsErrors(
        string effectType,
        string effectParameters)
    {
        var request = new CreateLocationRequest
        {
            Name = "Ubicación",
            Description = "Descripción",
            MaxCards = 3,
            EnergyMode = "Fixed",
            ExtraEnergy = 0,
            EffectType = effectType,
            EffectParameters = effectParameters
        };

        Assert.NotEmpty(LocationValidator.Validate(request));
    }
}
