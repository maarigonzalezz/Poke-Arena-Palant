using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using PokeArena.Domain.Entities;
using PokeArena.Domain.Catalogs;
using PokeArena.Application.Inventory;
using PokeArena.Infrastructure.Persistence;
using PokeArena.Application.Cards;

namespace PokeArena.Infrastructure.Inventory;

// Conserva la atomicidad y los bloqueos; Application decide cuándo y qué entregar.
public sealed class PostgresInventoryRepository(PokeArenaDbContext db, ILogger<PostgresInventoryRepository> logger)
    : IInventoryRepository
{
    private static CardItem CardI(Card card, AbilityRecord ability) => new(
        card.Id, card.Code, card.Name, card.Cost, card.Power, card.CardType, card.Rarity,
        card.ImageStatus, card.ImageOriginalPath, card.ImageSmallPath, ability.Code, ability.Name,
        ability.TriggerType, ability.EffectType, ability.Parameters);

    public Task<Player?> FindPlayerAsync(Guid playerId, CancellationToken cancellationToken) =>
        db.Players.AsNoTracking().SingleOrDefaultAsync(player => player.Id == playerId, cancellationToken);

    public async Task<InventorySnapshot> ReadAsync(Guid playerId, CancellationToken cancellationToken)
    {
        // Saldo, cartas y marca pertenecen a la misma instantánea.
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
        var player = await db.Players.AsNoTracking().SingleOrDefaultAsync(p => p.Id == playerId, cancellationToken);
        var owned = await (
            from entry in db.PlayerCards.AsNoTracking()
            where entry.PlayerId == playerId
            join card in db.Cards.AsNoTracking() on entry.CardId equals card.Id
            join ability in db.Abilities.AsNoTracking() on card.AbilityCode equals ability.Code
            orderby card.Id
            select CardI(card, ability)
        ).ToListAsync(cancellationToken);
        var activeIds = await (
            from entry in db.PlayerCards.AsNoTracking()
            join card in db.Cards.AsNoTracking() on entry.CardId equals card.Id
            where entry.PlayerId == playerId && card.Active
            orderby card.Id
            select card.Id
        ).ToListAsync(cancellationToken);
        var state = await ReadState(playerId, player, cancellationToken);
        await snapshot.CommitAsync(cancellationToken);
        return new(state, owned, activeIds);
    }

    public async Task<T> WithPlayerLockAsync<T>(Guid playerId,
        Func<IInventoryWriteSession, CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            // El bloqueo dura hasta confirmar la escritura o terminar la decisión sin cambios.
            var player = await db.Players.FromSqlInterpolated(
                $"SELECT * FROM players WHERE id={playerId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
            if (player is not null) await db.Entry(player).ReloadAsync(cancellationToken);
            var result = await operation(new WriteSession(this, playerId, player), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is DbUpdateException or NpgsqlException)
        {
            db.ChangeTracker.Clear();
            logger.LogWarning(ex, "Falló la transacción de inventario del jugador {PlayerId}.", playerId);
            throw new InventoryPersistenceException(ex);
        }
    }

    private async Task<InventoryState> ReadState(Guid playerId, Player? player, CancellationToken token) => new(
        player,
        await db.PlayerCubeMovements.AnyAsync(x => x.PlayerId == playerId && x.Reason == GameRules.InitialCollectionReason, token),
        await db.PlayerCards.AnyAsync(x => x.PlayerId == playerId, token),
        await db.PlayerCubeMovements.AnyAsync(x => x.PlayerId == playerId, token));

    public async Task<IReadOnlyList<CubeMovementItem>> ReadHistoryAsync(Guid playerId, int after, int take, CancellationToken cancellationToken) =>
        await db.PlayerCubeMovements.AsNoTracking()
            .Where(x => x.PlayerId == playerId && x.Sequence > after).OrderBy(x => x.Sequence).Take(take)
            .Select(x => new CubeMovementItem(x.Sequence, x.Amount, x.BalanceAfter, x.Reason, x.CreatedAt))
            .ToListAsync(cancellationToken);

    // Esta sesión solo existe dentro de la transacción abierta por WithPlayerLockAsync.
    private sealed class WriteSession(PostgresInventoryRepository owner, Guid playerId, Player? player) : IInventoryWriteSession
    {
        public Task<InventoryState> ReadStateAsync(CancellationToken token) => owner.ReadState(playerId, player, token);

        public Task<List<long>> ReadCardIdsAsync(string rarity, bool active, CancellationToken token) =>
            owner.db.Cards.AsNoTracking().Where(card => card.Active == active && card.Rarity == rarity)
                .OrderBy(card => card.Id).Select(card => card.Id).ToListAsync(token);

        public async Task SaveGrantAsync(InitialCollectionGrant grant, CancellationToken token)
        {
            foreach (var id in grant.CardIds)
                owner.db.PlayerCards.Add(new PlayerCard { PlayerId = playerId, CardId = id });
            player!.Cubes = grant.Cubes;
            owner.db.PlayerCubeMovements.Add(new PlayerCubeMovement {
                PlayerId = playerId, Sequence = grant.Sequence, Amount = grant.Cubes,
                BalanceAfter = grant.Cubes, Reason = grant.Reason
            });
            await owner.db.SaveChangesAsync(token);
        }
    }

    private readonly PokeArenaDbContext db = db;
}
