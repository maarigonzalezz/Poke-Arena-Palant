using PokeArena.Application.Authentication;
using PokeArena.Application.Cards;

namespace PokeArena.Application.Inventory;


/// <summary>Perfil de un jugador usado para realizar peticiones</summary>
public sealed record PlayerProfileQuery
{
    public required PlayerProfile Profile {get; init;}
    public required bool NewPlayer {get; init;}
}

/// <summary>Resultado de consulta. Perfil del jugador con su inventario actual</summary>
public sealed record PlayerProfileResult (
    PlayerProfile Profile,
    bool NewPlayer,
    IEnumerable<CardItem> Collection
);