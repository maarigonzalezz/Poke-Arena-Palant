namespace PokeArena.Infrastructure.Persistence;

// Proyección de lectura acordada con mazos; una fila nula de carta representa un deck vacío.
internal sealed class MatchmakingDeckRow
{
    public required string DeckId { get; set; }
    public Guid PlayerId { get; set; }
    public required string Name { get; set; }
    public long? CardId { get; set; }
}
