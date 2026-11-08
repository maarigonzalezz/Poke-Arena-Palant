
namespace PokeArena.Infrastructure.Persistence;

/// <summary>Modelo de una moneda virtual vinculada a un jugador.</summary>
public sealed class PlayerCards
{
    public Guid PlayerId { get; init; }
    public long CardId { get; init; }
}