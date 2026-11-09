
namespace PokeArena.Infrastructure.Persistence;

/// <summary>Relaciona un jugador con una carta y la cantidad de copias que posee.</summary>
public sealed class PlayerCard
{
    public Guid PlayerId { get; init; }
    public long CardId { get; init; }
    public int Copies { get; set; } = 1;
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
