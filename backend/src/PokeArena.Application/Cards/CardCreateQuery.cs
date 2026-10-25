
namespace PokeArena.Application.Cards;

/// <summary>Propiedades para crear carta.</summary>
public sealed record CardCreateQuery {

    public required string Code { get; init; }

    public required string Name { get; init; }

    public required int Cost { get; init; }

    public required int Power { get; init; }

    public required string Type { get; init; }

    public required string Rarity { get; init; }

    public required string AbilityCode { get; init; }

    public required int Version { get; init; }

}

/// <summary>
/// Propiedades de una imagen cargada
/// </summary>
public sealed record CardFileImage{

    public required string Name;

    public required string Format;

    public int vProportion;

    public int hProportion;

    public int vPixels;

    public int hPixels;
}