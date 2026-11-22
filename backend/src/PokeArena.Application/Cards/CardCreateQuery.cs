namespace PokeArena.Application.Cards;

/// <summary>Datos editables de una carta; código y versión se asignan en el servidor.</summary>
public sealed record CardCreateQuery
{
    public required string Name { get; init; }
    public required int Cost { get; init; }
    public required int Power { get; init; }
    public required string Type { get; init; }
    public required string Rarity { get; init; }
    public required string AbilityCode { get; init; }
}
