
namespace PokeArena.Domain.Entities;

/// <summary>
/// Representa una ubicación del juego con sus reglas, efecto y estado vigente.
/// </summary>
/// <remarks>
/// La ubicación mantiene una versión actual para identificar su configuración
/// vigente. Las versiones anteriores se conservan mediante instantáneas
/// almacenadas en la tabla location_versions.
/// </remarks>
public sealed class Location
{
    /// <summary>
    /// Obtiene el identificador único de la ubicación.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// Obtiene el nombre visible de la ubicación.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Obtiene la descripción de la ubicación y sus características.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Obtiene la cantidad máxima de cartas permitidas en la ubicación.
    /// </summary>
    public int MaxCards { get; init; }

    /// <summary>
    /// Obtiene el modo de cálculo de la energía adicional.
    /// Los valores admitidos son Fixed y PerCard.
    /// </summary>
    public required string EnergyMode { get; init; }

    /// <summary>
    /// Obtiene la cantidad de energía adicional según el modo configurado.
    /// </summary>
    public int ExtraEnergy { get; init; }

    /// <summary>
    /// Obtiene el identificador del tipo de efecto aplicado por la ubicación.
    /// </summary>
    public required string EffectType { get; init; }

    /// <summary>
    /// Obtiene los parámetros del efecto en formato JSON serializado.
    /// Su estructura depende del tipo de efecto configurado.
    /// </summary>
    public required string EffectParameters { get; init; }

    /// <summary>
    /// Obtiene el estado de procesamiento de la imagen de la ubicación.
    /// </summary>
    public required string ImageStatus { get; init; }

    /// <summary>
    /// Obtiene la ruta o referencia de la imagen original, si existe.
    /// </summary>
    public string? ImageOriginalPath { get; init; }

    /// <summary>
    /// Obtiene la ruta o referencia de la imagen reducida, si existe.
    /// </summary>
    public string? ImageSmallPath { get; init; }

    /// <summary>
    /// Obtiene el número de la versión vigente de la ubicación.
    /// </summary>
    public int CurrentVersion { get; init; }

    /// <summary>
    /// Indica si la ubicación está habilitada para su uso normal.
    /// </summary>
    public bool Active { get; init; }
}
