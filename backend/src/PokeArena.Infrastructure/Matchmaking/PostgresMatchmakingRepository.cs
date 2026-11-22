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

    public Task<MatchSearch?> ReadCurrentAsync(Guid playerId, CancellationToken cancellationToken) =>
        db.MatchSearches.AsNoTracking().Where(x => x.PlayerId == playerId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken);

    private sealed class Session(PokeArenaDbContext db) : IMatchmakingSession
    {
        public async Task<Player?> LockPlayerAsync(Guid playerId, CancellationToken token)
        {
            var player = await db.Players.FromSqlInterpolated($"SELECT * FROM players WHERE id={playerId} FOR UPDATE")
                .SingleOrDefaultAsync(token);
            if (player is not null) await db.Entry(player).ReloadAsync(token);
            return player;
        }

        public Task<MatchSearch?> FindOpenSearchAsync(Guid playerId, CancellationToken token) =>
            db.MatchSearches.SingleOrDefaultAsync(x => x.PlayerId == playerId && x.Status == MatchmakingRules.Searching, token);

        public Task<MatchSearch?> FindSearchAsync(Guid id, Guid playerId, CancellationToken token) =>
            db.MatchSearches.SingleOrDefaultAsync(x => x.Id == id && x.PlayerId == playerId, token);

        public Task<bool> HasActiveMatchAsync(Guid playerId, CancellationToken token) =>
            db.MatchPlayers.AnyAsync(x => x.PlayerId == playerId && x.Active, token);

        public async Task<bool> IsDeckReservedAsync(Guid playerId, string deckId, CancellationToken token) =>
            await db.MatchSearches.AnyAsync(x => x.PlayerId == playerId && x.DeckId == deckId && x.Status == MatchmakingRules.Searching, token)
            || await db.MatchPlayers.AnyAsync(x => x.PlayerId == playerId && x.DeckId == deckId && x.Active, token);

        public async Task<IReadOnlyList<long>> ReadOwnedCardIdsAsync(Guid playerId, CancellationToken token) =>
            await db.PlayerCards.AsNoTracking().Where(x => x.PlayerId == playerId && x.Copies > 0)
                .Select(x => x.CardId).ToListAsync(token);

        public async Task<IReadOnlyList<MatchSearch>> ReadWaitingAsync(string context, CancellationToken token) =>
            await db.MatchSearches.Where(x => x.Status == MatchmakingRules.Searching && x.Context == context)
                .OrderBy(x => x.LastCheckedAt).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id)
                .Take(MatchmakingRules.ProcessingBatchSize).ToListAsync(token);

        public async Task<IReadOnlyList<MatchSearch>> ReadCandidatesAsync(MatchSearch search, int range, CancellationToken token) =>
            await db.MatchSearches.Where(x => x.Status == MatchmakingRules.Searching && x.PlayerId != search.PlayerId
                && x.Context == search.Context && x.ContextId == search.ContextId
                && (long)x.Elo >= (long)search.Elo - range && (long)x.Elo <= (long)search.Elo + range)
                .OrderBy(x => Math.Abs((long)x.Elo - search.Elo)).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(token);

        public void AddSearch(MatchSearch search) => db.MatchSearches.Add(search);

        public void AddMatch(GameMatch match, MatchPlayer first, MatchPlayer second)
        {
            db.Matches.Add(match);
            db.MatchPlayers.AddRange(first, second);
        }
    }
}
