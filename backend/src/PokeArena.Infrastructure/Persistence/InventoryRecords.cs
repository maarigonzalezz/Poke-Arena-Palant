
namespace PokeArena.Infrastructure.Persistence;

/// <summary>Modelo de una moneda virtual vinculada a un jugador.</summary>
public sealed class PlayerCard
{
    public Guid PlayerId { get; init; }
    public long CardId { get; init; }
}

// El primer movimiento acredita la colección inicial y demuestra que ya fue entregada.
internal sealed class PlayerCubeMovement
{
    public Guid PlayerId { get; set; }
    public int Sequence { get; set; }
    public int Amount { get; set; }
    public int BalanceAfter { get; set; }
    public required string Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}
