namespace PokeArena.Domain.Matchmaking;

// Representa el encuentro creado; la ejecución de turnos pertenece al futuro motor de partidas.
public sealed class GameMatch
{
    public Guid Id { get; set; }
    public required string Context { get; set; }
    public Guid? ContextId { get; set; }
    public string Status { get; set; } = MatchmakingRules.ActiveMatch;
    public DateTime CreatedAt { get; set; }
    public bool IsBot { get; set; }
    // El bot usa una copia del deck fijado al entrar, sin crear jugadores artificiales.
    public long[]? BotCardIds { get; set; }
}

// La reserva activa impide que un jugador participe simultáneamente en dos partidas.
public sealed class MatchPlayer
{
    public Guid MatchId { get; set; }
    public Guid PlayerId { get; set; }
    public required string DeckId { get; set; }
    public required long[] CardIds { get; set; }
    public int Elo { get; set; }
    public bool Active { get; set; } = true;
}
