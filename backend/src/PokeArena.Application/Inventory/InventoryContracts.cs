
using PokeArena.Application.Authentication;

namespace PokeArena.Application.Inventory;

/// <summary>Moneda que posee un jugador.</summary>
public sealed record PlayerOwnedCurrency(
    long Id,
    string? Name,
    long Amount
);

/// <summary>Carta que posee un jugador.</summary>
public sealed record PlayerOwnedCard
(
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

/// <summary>Collection de cartas de un jugador</summary>
public sealed record PlayerCollection (
    IEnumerable<PlayerOwnedCard> Cards
);

/// <summary>Billetera virtual de un jugador</summary>
public sealed record PlayerWallet (
    IEnumerable<PlayerOwnedCurrency> Currencies
);

/// <summary>Inventario de un jugador.</summary>
public sealed record PlayerInventory(
    PlayerWallet Wallet,
    PlayerCollection Collection
);

/// <summary>Resultado de consulta. Perfil del jugador con su inventario actual</summary>
public sealed record PlayerProfileResult (
    PlayerProfile Profile,
    bool NewPlayer,
    PlayerInventory Inventory
);