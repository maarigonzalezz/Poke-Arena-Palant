using PokeArena.Domain.Entities;
using PokeArena.Domain.Matchmaking;

namespace PokeArena.Application.Matchmaking;

public sealed record MatchmakingDeck(string Id, Guid OwnerId, IReadOnlyList<long> CardIds);
public sealed record MatchmakingDeckOption(string Id, string Name);
public sealed record StartMatchSearchRequest(string? DeckId);
public sealed record MatchSearchResult(Guid Id, string Status, string Context, Guid? ContextId,
    string DeckId, int Elo, int AcceptedEloDifference, DateTime CreatedAt, Guid? MatchId, string? Message, bool BotAvailable);
public sealed record MatchLocationState(int Slot, long Id, string Name, string Description, int MaxCards,
    string EnergyMode, int ExtraEnergy, string EffectType, IReadOnlyList<int> RestrictedTurns, string? ImageSmallPath);
public sealed record MatchCardState(long Id, string Name, int Cost, int Power, string CardType,
    string? ImagePath, string? ImageSmallPath);
public sealed record MatchBoardCardState(long Id, string Name, int Cost, int Power, int LocationSlot, string? ImagePath);
public sealed record MatchPlayerState(Guid PlayerId, bool IsBot, IReadOnlyList<MatchCardState> HandCards,
    IReadOnlyList<long> CemeteryCardIds, IReadOnlyList<MatchCardState> CemeteryCards,
    IReadOnlyList<MatchBoardCardState> BoardCards,
    int Energy, int RemainingDeckCards);
public sealed record MatchOpponentState(bool IsBot, int HandCardCount, IReadOnlyList<MatchCardState> CemeteryCards,
    IReadOnlyList<MatchBoardCardState> BoardCards, int Energy, int RemainingDeckCards);
public sealed record MatchResultState(string Outcome, int PlayerLocationsWon, int OpponentLocationsWon,
    int PlayerTotalPower, int OpponentTotalPower, int PlayerFinalEnergy, int OpponentFinalEnergy,
    int PlayerCemeteryCount, int OpponentCemeteryCount, int CubesAwarded);
public sealed record DiscardMatchCardRequest(long CardId);
public sealed record AdvanceMatchTurnRequest(int TurnNumber);
public sealed record MatchStateResult(Guid MatchId, string Context, string Status, bool IsBot,
    int TurnNumber, int TurnDurationSeconds, DateTime? TurnStartedAt, DateTime? TurnEndsAt,
    int WagerCubes, IReadOnlyList<MatchLocationState> Locations, MatchPlayerState Player,
    MatchOpponentState Opponent, MatchResultState? Result);
public sealed record PlayMatchCardRequest(long CardId, int LocationSlot);

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
    Task<IReadOnlyList<MatchSearch>> ReadInactiveAsync(DateTime cutoff, CancellationToken cancellationToken);
    Task<IReadOnlyList<MatchSearch>> ReadCandidatesAsync(MatchSearch search, int range, CancellationToken cancellationToken);
    Task<IReadOnlyList<Location>> ReadMatchLocationsAsync(CancellationToken cancellationToken);
    Task<GameMatch?> LockMatchAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MatchPlayer>> ReadMatchPlayersAsync(Guid matchId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MatchLocationChoice>> ReadMatchLocationChoicesAsync(Guid matchId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Card>> ReadCardsAsync(IReadOnlyCollection<long> cardIds, CancellationToken cancellationToken);
    Task AddSearchAsync(MatchSearch search, CancellationToken cancellationToken);
    void AddMatch(GameMatch match, params MatchPlayer[] participants);
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
