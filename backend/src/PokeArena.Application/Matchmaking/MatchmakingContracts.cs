using PokeArena.Domain.Entities;
using PokeArena.Domain.Matchmaking;

namespace PokeArena.Application.Matchmaking;

public sealed record MatchmakingDeck(string Id, Guid OwnerId, IReadOnlyList<long> CardIds);
public sealed record MatchmakingDeckOption(string Id, string Name);
public sealed record StartMatchSearchRequest(string? DeckId);
public sealed record MatchSearchResult(Guid Id, string Status, string Context, Guid? ContextId,
    string DeckId, int Elo, int AcceptedEloDifference, DateTime CreatedAt, Guid? MatchId, string? Message, bool BotAvailable);

// Mazos aporta lectura real de propietario y cartas; no se requiere su API de creación.
public interface IMatchmakingDeckReader
{
    // Devuelve todas las cartas, incluso duplicadas, y conserva un deck vacío como lista vacía.
    Task<MatchmakingDeck?> FindAsync(string deckId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MatchmakingDeckOption>> ListAsync(Guid playerId, CancellationToken cancellationToken);
}

public interface IMatchmakingSession
{
    Task<Player?> LockPlayerAsync(Guid playerId, CancellationToken cancellationToken);
    Task<MatchSearch?> FindOpenSearchAsync(Guid playerId, CancellationToken cancellationToken);
    Task<MatchSearch?> FindSearchAsync(Guid searchId, Guid playerId, CancellationToken cancellationToken);
    Task<bool> HasActiveMatchAsync(Guid playerId, CancellationToken cancellationToken);
    Task<bool> IsDeckReservedAsync(Guid playerId, string deckId, CancellationToken cancellationToken);
    Task<IReadOnlyList<long>> ReadOwnedCardIdsAsync(Guid playerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MatchSearch>> ReadWaitingAsync(string context, CancellationToken cancellationToken);
    Task<IReadOnlyList<MatchSearch>> ReadCandidatesAsync(MatchSearch search, int range, CancellationToken cancellationToken);
    void AddSearch(MatchSearch search);
    void AddMatch(GameMatch match, MatchPlayer first, MatchPlayer second);
}

// Mazos envuelve edición y borrado con este guard y usa el DbContext del mismo alcance.
public interface IMatchmakingDeckGuard
{
    Task<T> WithUnlockedDeckAsync<T>(Guid playerId, string deckId,
        Func<CancellationToken, Task<T>> edit, CancellationToken cancellationToken);
}

public interface IMatchmakingRepository
{
    // La transacción serializa admisión, cancelación y emparejamiento entre instancias.
    Task<T> InTransactionAsync<T>(Func<IMatchmakingSession, CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
    Task<MatchSearch?> ReadCurrentAsync(Guid playerId, CancellationToken cancellationToken);
}

public sealed class MatchmakingFailure(string code, string message, int statusCode,
    object? details = null) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public object? Details { get; } = details;
}
