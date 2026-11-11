using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using PokeArena.Domain.Entities;
using PokeArena.Application.Inventory;
using PokeArena.Infrastructure.Persistence;
using PokeArena.Application.Cards;
using PokeArena.Application.Authentication;

namespace PokeArena.Infrastructure.Inventory;

public sealed class PostgresInventoryRepository(PokeArenaDbContext db, ILogger<PostgresInventoryRepository> logger)
    : IInventoryRepository
{
    private const string InitialReason = "InitialCollection";
    private const int InitialCollectionSize = 15;
    private const int StarterDeckSize = 12;

    private static CardItem CardI(Card card, AbilityRecord ability) => new(
        card.Id, card.Code, card.Name, card.Cost, card.Power, card.CardType, card.Rarity,
        card.ImageStatus, card.ImageOriginalPath, card.ImageSmallPath, ability.Code, ability.Name,
        ability.TriggerType, ability.EffectType, ability.Parameters);

    private static PlayerProfile Profile(Player player) =>
        new(player.Id, player.DisplayName, player.Email, player.Role, player.Elo, player.Cubes);

    // Solo devuelve datos persistidos; un GET no acredita cubos ni entrega cartas.
    public async Task<PlayerProfileResult> ConsultAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        if (playerId == Guid.Empty)
            throw new InventoryValidationException(new() { ["playerId"] = "Jugador inválido." });
        // Saldo y cartas se leen de la misma instantánea aunque otra solicitud termine la entrega.
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
        var player = await db.Players.AsNoTracking().SingleOrDefaultAsync(p => p.Id == playerId, cancellationToken);
        EnsureActive(player);
        var owned = await (
            from entry in db.PlayerCards.AsNoTracking()
            where entry.PlayerId == playerId
            join card in db.Cards.AsNoTracking() on entry.CardId equals card.Id
            join ability in db.Abilities.AsNoTracking() on card.AbilityCode equals ability.Code
            orderby card.Id
            select CardI(card, ability)
        ).ToListAsync(cancellationToken);
        var completed = await WasDelivered(playerId, cancellationToken);
        var status = completed ? new InitialCollectionResult("Completed")
            : new InitialCollectionResult("Pending", "La entrega inicial se intentará en el próximo ingreso.");
        var deck = await (
            from entry in db.PlayerCards.AsNoTracking()
            join card in db.Cards.AsNoTracking() on entry.CardId equals card.Id
            where entry.PlayerId == playerId && card.Active
            orderby card.Id
            select card.Id
        ).Take(StarterDeckSize).ToListAsync(cancellationToken);
        // El mazo usa doce cartas; no sustituye el requisito de quince para la entrega.
        var result = new PlayerProfileResult(Profile(player!), false, owned, status,
            deck.Distinct().Count() == StarterDeckSize ? deck : []);
        await snapshot.CommitAsync(cancellationToken);
        return result;
    }

    // Un fallo revierte la entrega completa y permite volver a intentarla sin perder la cuenta.
    public async Task<InitialCollectionResult> EnsureInitialCollectionAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        if (playerId == Guid.Empty)
            throw new InventoryValidationException(new() { ["playerId"] = "Jugador inválido." });
        try { return await DeliverAsync(playerId, cancellationToken); }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is DbUpdateException or NpgsqlException)
        {
            db.ChangeTracker.Clear();
            logger.LogWarning(ex, "Entrega inicial pendiente para el jugador {PlayerId}.", playerId);
            return new InitialCollectionResult("PendingRetry", "No se pudo completar la entrega inicial; se reintentará en el próximo ingreso.");
        }
    }

    private async Task<InitialCollectionResult> DeliverAsync(Guid playerId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // El bloqueo serializa dos ingresos del mismo jugador, incluso desde servidores distintos.
        var player = await db.Players.FromSqlInterpolated(
            $"SELECT * FROM players WHERE id={playerId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (player is not null)
            await db.Entry(player).ReloadAsync(cancellationToken);
        EnsureActive(player);

        if (await WasDelivered(playerId, cancellationToken))
            return new InitialCollectionResult("Completed");

        // No inventa un historial ni vuelve a premiar inventarios creados por la versión anterior.
        if (player!.Cubes != 0 || await db.PlayerCards.AnyAsync(x => x.PlayerId == playerId, cancellationToken)
            || await db.PlayerCubeMovements.AnyAsync(x => x.PlayerId == playerId, cancellationToken))
            return new InitialCollectionResult("LegacyReview", "La cuenta tiene un inventario previo sin registro de entrega inicial; requiere revisión.");

        // Conserva las cartas Pending del catálogo de prueba y carga solo sus identificadores.
        var available = await db.Cards.AsNoTracking()
            .Where(card => card.Active && card.Rarity == "Común")
            .OrderBy(card => card.Id).Select(card => card.Id).ToListAsync(cancellationToken);
        if (available.Count < InitialCollectionSize)
            return new InitialCollectionResult("PendingCatalog", "No hay suficientes cartas comunes; el juego aún no está disponible.");

        var chosen = new List<long>(InitialCollectionSize);
        while (chosen.Count < InitialCollectionSize)
        {
            var index = Random.Shared.Next(available.Count);
            chosen.Add(available[index]);
            available.RemoveAt(index);
        }
        // Comprueba la entrega completa antes de escribir cartas, saldo o historial.
        if (chosen.Count != InitialCollectionSize || chosen.Distinct().Count() != InitialCollectionSize)
            throw new InvalidOperationException("La colección inicial debe contener quince cartas distintas.");

        foreach (var cardId in chosen)
            db.PlayerCards.Add(new PlayerCard { PlayerId = playerId, CardId = cardId });

        player.Cubes = 1000;
        // Este movimiento es a la vez el primer abono y la marca persistente de entrega.
        db.PlayerCubeMovements.Add(new PlayerCubeMovement {
            PlayerId = playerId, Sequence = 1, Amount = 1000, BalanceAfter = 1000, Reason = InitialReason
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new InitialCollectionResult("Completed");
    }

    // La marca no depende del saldo actual ni del número de cartas que aún conserve el jugador.
    private Task<bool> WasDelivered(Guid playerId, CancellationToken cancellationToken) =>
        db.PlayerCubeMovements.AnyAsync(x => x.PlayerId == playerId && x.Reason == InitialReason, cancellationToken);

    public async Task<CubeHistoryResult> GetCubeHistoryAsync(Guid playerId, int after, CancellationToken cancellationToken = default)
    {
        if (playerId == Guid.Empty)
            throw new InventoryValidationException(new() { ["playerId"] = "Jugador inválido." });
        if (after < 0)
            throw new InventoryValidationException(new() { ["after"] = "Debe ser cero o positivo." });
        EnsureActive(await db.Players.AsNoTracking().SingleOrDefaultAsync(x => x.Id == playerId, cancellationToken));
        // El cursor acota cada respuesta sin omitir el movimiento inicial de la primera página.
        var rows = await db.PlayerCubeMovements.AsNoTracking()
            .Where(x => x.PlayerId == playerId && x.Sequence > after).OrderBy(x => x.Sequence).Take(51)
            .Select(x => new CubeMovementItem(x.Sequence, x.Amount, x.BalanceAfter, x.Reason, x.CreatedAt))
            .ToListAsync(cancellationToken);
        return new CubeHistoryResult(rows.Take(50).ToList(), rows.Count > 50 ? rows[49].Sequence : null);
    }

    private static void EnsureActive(Player? player)
    {
        if (player is null) throw new AuthenticationFailure("account_not_found", "La cuenta ya no está disponible.", 401);
        if (!player.Active) throw new AuthenticationFailure("account_disabled", "La cuenta está deshabilitada.", 403);
    }
}
