using PokeArena.Domain.Decks;

namespace PokeArena.Application.Decks;

public interface IDeckRepository
{
    Task<IReadOnlyList<DeckResult>> ListAsync(Guid playerId, CancellationToken token);
    Task<DeckResult?> FindAsync(Guid playerId, Guid deckId, CancellationToken token);
    Task<DeckResult> CreateOnceAsync(Guid playerId, string key, string name, string[] cardCodes,
        Func<IDeckWriteSession, CancellationToken, Task<DeckResult>> create, CancellationToken token);
}

public interface IDeckWriteSession
{
    Task<IReadOnlyList<OwnedDeckCard>> ReadOwnedCardsAsync(string[] codes, CancellationToken token);
    void Add(PlayerDeck deck);
}
