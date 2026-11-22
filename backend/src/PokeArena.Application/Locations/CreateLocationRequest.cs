
namespace PokeArena.Application.Locations;

/// <summary>Datos editables recibidos del formulario; identidad e imágenes se asignan al guardar.</summary>
public sealed class CreateLocationRequest
{
    /// <summary>Nombre obligatorio de la ubicación.</summary>
    public required string Name { get; init; }

    /// <summary>Descripción obligatoria del escenario.</summary>
    public required string Description { get; init; }

    /// <summary>Capacidad por jugador entre 0 y 5.</summary>
    public int MaxCards { get; init; }

    /// <summary>Selecciona energía fija por turno o por carta propia.</summary>
    public required string EnergyMode { get; init; }

    /// <summary>El rango permitido depende de la modalidad de energía.</summary>
    public int ExtraEnergy { get; init; }

    /// <summary>None, TypeBonus, CostModifier o TurnRestriction.</summary>
    public required string EffectType { get; init; }

    /// <summary>JSON que LocationValidator verifica según el efecto.</summary>
    public string EffectParameters { get; init; } = "{}";
}
