
namespace PokeArena.Application.Cards;

/// <summary>Datos de una carta y su habilidad incluidos en los resultados.</summary>
public sealed record CardSearchItem(
    string Code,
    string Name,
    int Cost,
    int Power,
    string Type,
    string Rarity,
    string ImageStatus,
    string? ImageUrl,
    string AbilityCode,
    string AbilityName,
    string? AbilityTrigger,
    string? AbilityEffect,
    string AbilityParameters
);

/// <summary>Página de una sesión; NextPosition nulo indica el final de los resultados.</summary>
public sealed record CardSearchResult(
    IReadOnlyList<CardSearchItem> Items,
    Guid SearchId,
    long? NextPosition,
    long TotalCount
);
