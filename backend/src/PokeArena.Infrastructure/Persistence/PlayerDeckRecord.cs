namespace PokeArena.Infrastructure.Persistence;

internal sealed class PlayerDeckRecord
{
    public required string Id { get; set; }
    public Guid PlayerId { get; set; }
    public required string Name { get; set; }
    public long[] CardIds { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
