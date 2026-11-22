
namespace PokeArena.Domain.Entities;

/// <summary>Modelo de una carta con su habilidad, versión vigente y estado de imagen.</summary>
public sealed class Card
{
    public long Id { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public int Cost { get; init; }

    public int Power { get; init; }

    public required string CardType { get; init; }

    public required string Rarity { get; init; }

    public required string AbilityCode { get; init; }

    public required string ImageStatus { get; init; }

    public string? ImageOriginalPath { get; init; }

    public string? ImageSmallPath { get; init; }

    public int CurrentVersion { get; init; }

    public bool Active { get; init; }
}
