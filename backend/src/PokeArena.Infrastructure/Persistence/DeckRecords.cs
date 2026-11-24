namespace PokeArena.Infrastructure.Persistence;

internal sealed record PlayerDeckRecord(Guid Id, Guid PlayerId, string Name, string NameKey, DateTime CreatedAt);
internal sealed record PlayerDeckCardRecord(Guid DeckId, long CardId);
