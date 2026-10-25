
namespace PokeArena.Application.Locations;

/// <summary>
/// Representa los datos proporcionados para crear una ubicación.
/// </summary>
public sealed class CreateLocationRequest
{
    /// <summary>
    /// Obtiene o establece el nombre de la ubicación.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Obtiene o establece la descripción de la ubicación.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Obtiene o establece la cantidad máxima de cartas permitidas.
    /// </summary>
    public int MaxCards { get; init; }

    /// <summary>
    /// Obtiene o establece el modo de cálculo de energía adicional.
    /// </summary>
    public required string EnergyMode { get; init; }

    /// <summary>
    /// Obtiene o establece la cantidad de energía adicional.
    /// </summary>
    public int ExtraEnergy { get; init; }

    /// <summary>
    /// Obtiene o establece el tipo de efecto de la ubicación.
    /// </summary>
    public required string EffectType { get; init; }

    /// <summary>
    /// Obtiene o establece los parámetros del efecto en formato JSON.
    /// </summary>
    public string EffectParameters { get; init; } = "{}";
}
