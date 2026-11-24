using PokeArena.Domain.Entities;
using PokeArena.Domain.Matchmaking;

namespace PokeArena.Application.Matchmaking;

public sealed record MatchmakingDeck(string Id, IReadOnlyList<long> CardIds);
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
    /// <summary>Obtiene la copia completa del mazo para validarla y fijarla al entrar a la búsqueda.</summary>
    Task<MatchmakingDeck?> FindAsync(string deckId, CancellationToken cancellationToken);
    /// <summary>Lista únicamente opciones de mazos pertenecientes al jugador indicado.</summary>
    Task<IReadOnlyList<MatchmakingDeckOption>> ListAsync(Guid playerId, CancellationToken cancellationToken);
}

public interface IMatchmakingSession
{
    /// <summary>Bloquea la fila del jugador durante la transacción para serializar cambios de cuenta y matchmaking.</summary>
    Task<Player?> LockPlayerAsync(Guid playerId, CancellationToken cancellationToken);
    /// <summary>Busca la búsqueda abierta del jugador, si existe.</summary>
    Task<MatchSearch?> FindOpenSearchAsync(Guid playerId, CancellationToken cancellationToken);
    /// <summary>Busca una solicitud solo si pertenece al jugador indicado.</summary>
    Task<MatchSearch?> FindSearchAsync(Guid searchId, Guid playerId, CancellationToken cancellationToken);
    /// <summary>Indica si el jugador participa en una partida activa.</summary>
    Task<bool> HasActiveMatchAsync(Guid playerId, CancellationToken cancellationToken);
    /// <summary>Impide editar o borrar mazos que están reservados por una búsqueda o partida.</summary>
    Task<bool> IsDeckReservedAsync(Guid playerId, string deckId, CancellationToken cancellationToken);
    /// <summary>Lee identificadores de cartas que el jugador posee actualmente.</summary>
    Task<IReadOnlyList<long>> ReadOwnedCardIdsAsync(Guid playerId, CancellationToken cancellationToken);
    /// <summary>Lee el siguiente lote de búsquedas esperando en el contexto indicado.</summary>
    Task<IReadOnlyList<MatchSearch>> ReadWaitingAsync(string context, CancellationToken cancellationToken);
    /// <summary>Lee búsquedas que superaron el límite de inactividad.</summary>
    Task<IReadOnlyList<MatchSearch>> ReadInactiveAsync(DateTime cutoff, CancellationToken cancellationToken);
    /// <summary>Busca rivales candidatos del mismo contexto y rango Elo.</summary>
    Task<IReadOnlyList<MatchSearch>> ReadCandidatesAsync(MatchSearch search, int range, CancellationToken cancellationToken);
    /// <summary>Lee ubicaciones disponibles para preparar una partida.</summary>
    Task<IReadOnlyList<Location>> ReadMatchLocationsAsync(CancellationToken cancellationToken);
    /// <summary>Bloquea una partida únicamente para uno de sus participantes.</summary>
    Task<GameMatch?> LockMatchAsync(Guid matchId, Guid playerId, CancellationToken cancellationToken);
    /// <summary>Lee todos los participantes de una partida para aplicar turnos y resultados.</summary>
    Task<IReadOnlyList<MatchPlayer>> ReadMatchPlayersAsync(Guid matchId, CancellationToken cancellationToken);
    /// <summary>Lee las ubicaciones elegidas y sus snapshots en orden de tablero.</summary>
    Task<IReadOnlyList<MatchLocationChoice>> ReadMatchLocationChoicesAsync(Guid matchId, CancellationToken cancellationToken);
    /// <summary>Lee cartas necesarias para construir el estado público del tablero.</summary>
    Task<IReadOnlyList<Card>> ReadCardsAsync(IReadOnlyCollection<long> cardIds, CancellationToken cancellationToken);
    /// <summary>Programa la búsqueda para guardarse en la transacción actual.</summary>
    Task AddSearchAsync(MatchSearch search, CancellationToken cancellationToken);
    /// <summary>Programa la partida, sus participantes y ubicaciones para guardarse juntos.</summary>
    void AddMatch(GameMatch match, params MatchPlayer[] participants);
}

// Mazos envuelve edición y borrado con este guard y usa el DbContext del mismo alcance.
public interface IMatchmakingDeckGuard
{
    /// <summary>Ejecuta una edición solo si el deck no está reservado por una búsqueda o partida activa.</summary>
    Task<T> WithUnlockedDeckAsync<T>(Guid playerId, string deckId,
        Func<CancellationToken, Task<T>> edit, CancellationToken cancellationToken);
}

public interface IMatchmakingRepository
{
    // La transacción serializa admisión, cancelación y emparejamiento entre instancias.
    /// <summary>Ejecuta una operación de aplicación en una transacción con bloqueo compartido.</summary>
    Task<T> InTransactionAsync<T>(Func<IMatchmakingSession, CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
    /// <summary>Lee la búsqueda más reciente para recuperar el estado tras recargar la interfaz.</summary>
    Task<MatchSearch?> ReadCurrentAsync(Guid playerId, CancellationToken cancellationToken);
}

public sealed class MatchmakingFailure(string code, string message, int statusCode,
    object? details = null) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public object? Details { get; } = details;
}
