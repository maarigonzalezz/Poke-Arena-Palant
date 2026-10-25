namespace PokeArena.Application.Cards;

/// <summary>Datos de una carta y su habilidad incluidos en los resultados.</summary>
public sealed record CardCreateItem(
    long Id,
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
    string AbilityParameters,
    string ImageSmallUrl
);

/// <summary>Resultado de crear una carta.</summary>
public sealed record CardCreateResult(
    CardCreateItem Item
);
