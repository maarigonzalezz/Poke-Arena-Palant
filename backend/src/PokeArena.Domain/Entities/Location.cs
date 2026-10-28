
namespace PokeArena.Domain.Entities;

/// <summary>Configuración persistida de una ubicación; sus versiones se guardan como instantáneas.</summary>
public sealed class Location
{
    /// <summary>Identificador generado por PostgreSQL.</summary>
    public long Id { get; init; }

    /// <summary>Nombre mostrado al administrador.</summary>
    public required string Name { get; init; }

    /// <summary>Descripción del escenario y sus condiciones.</summary>
    public required string Description { get; init; }

    /// <summary>Capacidad por jugador: cero impide jugar cartas en esta ubicación.</summary>
    public int MaxCards { get; init; }

    /// <summary>Fixed otorga energía fija; PerCard la calcula por carta propia.</summary>
    public required string EnergyMode { get; init; }

    /// <summary>Energía adicional: 0 a 3 en Fixed y 0 a 2 en PerCard.</summary>
    public int ExtraEnergy { get; init; }

    /// <summary>Selecciona la regla que interpretará el motor de partidas.</summary>
    public required string EffectType { get; init; }

    /// <summary>JSON con los parámetros específicos del efecto seleccionado.</summary>
    public required string EffectParameters { get; init; }

    /// <summary>Ready indica que se guardaron el original y la miniatura.</summary>
    public required string ImageStatus { get; init; }

    /// <summary>Ruta pública del original; el archivo se almacena fuera de PostgreSQL.</summary>
    public string? ImageOriginalPath { get; init; }

    /// <summary>Ruta de la miniatura de 320 × 240 generada por el servidor.</summary>
    public string? ImageSmallPath { get; init; }

    /// <summary>La creación inicia en versión 1 y conserva una instantánea.</summary>
    public int CurrentVersion { get; init; }

    /// <summary>La disponibilidad también exige imágenes completas y estado Ready.</summary>
    public bool Active { get; init; }
}
