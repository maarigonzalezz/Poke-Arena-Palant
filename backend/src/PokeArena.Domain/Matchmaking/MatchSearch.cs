namespace PokeArena.Domain.Matchmaking;

// El deck y sus cartas se fijan al entrar; los reintentos no cambian la selección.
public sealed class MatchSearch
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public required string DeckId { get; set; }
    public required long[] CardIds { get; set; }
    public required string Context { get; set; }
    public Guid? ContextId { get; set; }
    public int Elo { get; set; }
    public string Status { get; set; } = MatchmakingRules.Searching;
    public DateTime CreatedAt { get; set; }
    public DateTime LastCheckedAt { get; set; }
    public DateTime LastPolledAt { get; set; }
    public Guid? MatchId { get; set; }
    public string? EndReason { get; set; }
}
