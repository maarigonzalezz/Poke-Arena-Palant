
namespace PokeArena.Application.Cards;

/// <summary>Datos de una carta y su habilidad incluidos.</summary>
public sealed record CardItem(
    long Id,
    string Code,
    string Name,
    int Cost,
    int Power,
    string Type,
    string Rarity,
    string ImageStatus,
    string? ImageUrl,
    string? ImageSmallUrl,
    string  AbilityCode,
    string  AbilityName,
    string? AbilityTrigger,
    string? AbilityEffect,
    string  AbilityParameters
);