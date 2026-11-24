using Microsoft.EntityFrameworkCore;
using PokeArena.Application.Matchmaking;
using PokeArena.Domain.Entities;
using PokeArena.Domain.Matchmaking;
using PokeArena.Infrastructure.Persistence;

namespace PokeArena.Infrastructure.Matchmaking;

// La coordinación es de PostgreSQL, por lo que funciona con varios procesos de la API.
public sealed class PostgresMatchmakingRepository(PokeArenaDbContext db) : IMatchmakingRepository
{
    // Este bloqueo debe ser usado también por futuros productores de búsquedas de torneo.
    private const long QueueLockKey = 7065746573161;

    /// <summary>
    /// Ejecuta la operación con un bloqueo transaccional compartido entre instancias y confirma
    /// los cambios juntos; ante una excepción, la transacción se revierte.
    /// </summary>
    public async Task<T> InTransactionAsync<T>(Func<IMatchmakingSession, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({QueueLockKey})", cancellationToken);
        try
        {
            var result = await operation(new Session(db), cancellationToken);
            // Cola, partida y participantes se confirman juntos o se revierten juntos.
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            db.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>Lee, sin seguimiento de cambios, la búsqueda más reciente del jugador.</summary>
    public Task<MatchSearch?> ReadCurrentAsync(Guid playerId, CancellationToken cancellationToken) =>
        db.MatchSearches.AsNoTracking().Where(x => x.PlayerId == playerId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken);

    private sealed class Session(PokeArenaDbContext db) : IMatchmakingSession
    {
        /// <summary>Obtiene y bloquea la fila de jugador, recargando sus valores tras adquirir el bloqueo.</summary>
        public async Task<Player?> LockPlayerAsync(Guid playerId, CancellationToken token)
        {
            var player = await db.Players.FromSqlInterpolated($"SELECT * FROM players WHERE id={playerId} FOR UPDATE")
                .SingleOrDefaultAsync(token);
            if (player is not null) await db.Entry(player).ReloadAsync(token);
            return player;
        }

        /// <summary>Busca la única búsqueda abierta del jugador.</summary>
        public Task<MatchSearch?> FindOpenSearchAsync(Guid playerId, CancellationToken token) =>
            db.MatchSearches.SingleOrDefaultAsync(x => x.PlayerId == playerId && x.Status == MatchmakingRules.Searching, token);

        /// <summary>Limita la lectura de la búsqueda a su jugador propietario.</summary>
        public Task<MatchSearch?> FindSearchAsync(Guid id, Guid playerId, CancellationToken token) =>
            db.MatchSearches.SingleOrDefaultAsync(x => x.Id == id && x.PlayerId == playerId, token);

        /// <summary>Comprueba si existe una participación activa para el jugador.</summary>
        public Task<bool> HasActiveMatchAsync(Guid playerId, CancellationToken token) =>
            db.MatchPlayers.AnyAsync(x => x.PlayerId == playerId && x.Active, token);

        /// <summary>Comprueba reservas del mazo tanto en la cola abierta como en partidas activas.</summary>
        public async Task<bool> IsDeckReservedAsync(Guid playerId, string deckId, CancellationToken token) =>
            await db.MatchSearches.AnyAsync(x => x.PlayerId == playerId && x.DeckId == deckId && x.Status == MatchmakingRules.Searching, token)
            || await db.MatchPlayers.AnyAsync(x => x.PlayerId == playerId && x.DeckId == deckId && x.Active, token);

        /// <summary>Devuelve ids de cartas con al menos una copia en la colección del jugador.</summary>
        public async Task<IReadOnlyList<long>> ReadOwnedCardIdsAsync(Guid playerId, CancellationToken token) =>
            await db.PlayerCards.AsNoTracking().Where(x => x.PlayerId == playerId && x.Copies > 0)
                .Select(x => x.CardId).ToListAsync(token);

        /// <summary>Obtiene un lote acotado y ordenado de búsquedas abiertas del contexto solicitado.</summary>
        public async Task<IReadOnlyList<MatchSearch>> ReadWaitingAsync(string context, CancellationToken token) =>
            await db.MatchSearches.Where(x => x.Status == MatchmakingRules.Searching && x.Context == context)
                .OrderBy(x => x.LastCheckedAt).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id)
                .Take(MatchmakingRules.ProcessingBatchSize).ToListAsync(token);

        /// <summary>Obtiene un lote ordenado de búsquedas abiertas cuya última presencia venció.</summary>
        public async Task<IReadOnlyList<MatchSearch>> ReadInactiveAsync(DateTime cutoff, CancellationToken token) =>
            await db.MatchSearches.Where(x => x.Status == MatchmakingRules.Searching && x.LastPolledAt <= cutoff)
                .OrderBy(x => x.LastPolledAt).ThenBy(x => x.Id)
                .Take(MatchmakingRules.ProcessingBatchSize).ToListAsync(token);

        /// <summary>Filtra posibles rivales por contexto y Elo, priorizando menor diferencia y mayor antigüedad.</summary>
        public async Task<IReadOnlyList<MatchSearch>> ReadCandidatesAsync(MatchSearch search, int range, CancellationToken token) =>
            await db.MatchSearches.Where(x => x.Status == MatchmakingRules.Searching && x.PlayerId != search.PlayerId
                && x.Context == search.Context && x.ContextId == search.ContextId
                && (long)x.Elo >= (long)search.Elo - range && (long)x.Elo <= (long)search.Elo + range)
                .OrderBy(x => Math.Abs((long)x.Elo - search.Elo)).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(token);

        /// <summary>Lee ubicaciones activas con imágenes listas; Domain aplica después los requisitos de selección.</summary>
        public async Task<IReadOnlyList<Location>> ReadMatchLocationsAsync(CancellationToken token) =>
            await db.Locations.AsNoTracking()
                .Where(x => x.Active && x.ImageStatus == "Ready")
                .OrderBy(x => x.Id)
                .ToListAsync(token);

        /// <summary>Verifica participación antes de bloquear la partida para serializar acciones y avances de turno.</summary>
        public async Task<GameMatch?> LockMatchAsync(Guid matchId, Guid playerId, CancellationToken token)
        {
            var member = await db.MatchPlayers.AsNoTracking()
                .AnyAsync(x => x.MatchId == matchId && x.PlayerId == playerId, token);
            if (!member) return null;
            var match = await db.Matches.FromSqlInterpolated(
                    $"SELECT * FROM matches WHERE id={matchId} FOR UPDATE")
                .SingleOrDefaultAsync(token);
            if (match is not null) await db.Entry(match).ReloadAsync(token);
            return match;
        }

        /// <summary>Lee participantes para actualizar cartas, energía, cementerios y resultado.</summary>
        public async Task<IReadOnlyList<MatchPlayer>> ReadMatchPlayersAsync(Guid matchId, CancellationToken token) =>
            await db.MatchPlayers.Where(x => x.MatchId == matchId).ToListAsync(token);

        /// <summary>Lee las ubicaciones persistidas en orden de slot, incluyendo su configuración congelada.</summary>
        public async Task<IReadOnlyList<MatchLocationChoice>> ReadMatchLocationChoicesAsync(Guid matchId, CancellationToken token) =>
            await db.MatchLocationChoices.AsNoTracking().Where(x => x.MatchId == matchId)
                .OrderBy(x => x.Slot).ToListAsync(token);

        /// <summary>Lee en lote los datos de catálogo requeridos para presentar las cartas del match.</summary>
        public async Task<IReadOnlyList<Card>> ReadCardsAsync(IReadOnlyCollection<long> cardIds, CancellationToken token) =>
            await db.Cards.AsNoTracking().Where(x => cardIds.Contains(x.Id)).ToListAsync(token);

        /// <summary>Agrega una búsqueda después de guardar cambios previos necesarios dentro de la transacción.</summary>
        public async Task AddSearchAsync(MatchSearch search, CancellationToken token)
        {
            // Libera primero el índice de una búsqueda vencida dentro de la misma transacción.
            await db.SaveChangesAsync(token);
            db.MatchSearches.Add(search);
        }

        /// <summary>Agrega conjuntamente partida, participantes y ubicaciones elegidas al contexto de persistencia.</summary>
        public void AddMatch(GameMatch match, params MatchPlayer[] participants)
        {
            db.Matches.Add(match);
            db.MatchPlayers.AddRange(participants);
            db.MatchLocationChoices.AddRange(match.Locations);
        }
    }
}
