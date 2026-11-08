using PokeArena.Application.Authentication;

namespace PokeArena.Application.Inventory;

public sealed record PlayerProfileQuery
{
    public required PlayerProfile Profile {get; init;}
    public required bool NewPlayer {get; init;}
}