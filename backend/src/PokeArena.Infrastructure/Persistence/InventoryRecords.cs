
namespace PokeArena.Infrastructure.Persistence;

/// <summary>Modelo de una moneda virtual vinculada a un jugador.</summary>
public sealed class PlayerCard
{
    public Guid PlayerId { get; init; }
    public long CardId { get; init; }
}