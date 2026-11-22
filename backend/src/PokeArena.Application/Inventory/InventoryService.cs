using PokeArena.Application.Authentication;
using PokeArena.Domain.Catalogs;

namespace PokeArena.Application.Inventory;

// Decide las reglas de inventario; el repositorio solo consulta y persiste datos.
public sealed class InventoryService(IInventoryRepository repository)
{
    public async Task<PlayerProfileResult> ConsultAsync(Guid playerId, CancellationToken cancellationToken)
    {
        ValidatePlayerId(playerId);
        var snapshot = await repository.ReadAsync(playerId, cancellationToken);
        var player = PlayerAccess.RequireActive(snapshot.State.Player);
        var status = snapshot.State.Delivered ? new InitialCollectionResult("Completed")
            : new InitialCollectionResult("Pending", "La entrega inicial se intentará en el próximo ingreso.");
        var deck = snapshot.ActiveCardIds.Distinct().Take(GameRules.DeckSize).ToList();
        return new PlayerProfileResult(new(player.Id, player.DisplayName, player.Email, player.Role, player.Elo, player.Cubes),
            false, snapshot.Cards, status, deck.Count == GameRules.DeckSize ? deck : []);
    }

    public async Task<InitialCollectionResult> EnsureInitialCollectionAsync(Guid playerId, CancellationToken cancellationToken)
    {
        ValidatePlayerId(playerId);
        try
        {
            return await repository.WithPlayerLockAsync(playerId, async (session, token) =>
            {
                var state = await session.ReadStateAsync(token);
                var player = PlayerAccess.RequireActive(state.Player);
                if (state.Delivered) return new InitialCollectionResult("Completed");
                if (player.Cubes != 0 || state.HasCards || state.HasMovements)
                    return new InitialCollectionResult("LegacyReview", "La cuenta tiene un inventario previo sin registro de entrega inicial; requiere revisión.");

                // El catálogo de pruebas admite imágenes pendientes; solo importan rareza y actividad.
                var available = (await session.ReadCardIdsAsync(GameRules.CommonRarity, true, token)).Distinct().ToList();
                if (available.Count < GameRules.InitialCollectionSize)
                    return new InitialCollectionResult("PendingCatalog", "No hay suficientes cartas comunes; el juego aún no está disponible.");
                var chosen = new List<long>(GameRules.InitialCollectionSize);
                while (chosen.Count < GameRules.InitialCollectionSize)
                {
                    var index = Random.Shared.Next(available.Count);
                    chosen.Add(available[index]);
                    available.RemoveAt(index);
                }
                // La selección y el premio siempre proceden del servidor, nunca del formulario.
                await session.SaveGrantAsync(new(chosen, GameRules.InitialCubes, GameRules.InitialMovementSequence,
                    GameRules.InitialCollectionReason), token);
                return new InitialCollectionResult("Completed", GrantedNow: true);
            }, cancellationToken);
        }
        catch (InventoryPersistenceException) when (!cancellationToken.IsCancellationRequested)
        {
            return new InitialCollectionResult("PendingRetry", "No se pudo completar la entrega inicial; se reintentará en el próximo ingreso.");
        }
    }

    public async Task<CubeHistoryResult> GetCubeHistoryAsync(Guid playerId, int after, CancellationToken cancellationToken)
    {
        ValidatePlayerId(playerId);
        if (after < 0) throw new InventoryValidationException(new() { ["after"] = "Debe ser cero o positivo." });
        PlayerAccess.RequireActive(await repository.FindPlayerAsync(playerId, cancellationToken));
        var rows = await repository.ReadHistoryAsync(playerId, after, GameRules.CubeHistoryPageSize + 1, cancellationToken);
        return new(rows.Take(GameRules.CubeHistoryPageSize).ToList(),
            rows.Count > GameRules.CubeHistoryPageSize ? rows[GameRules.CubeHistoryPageSize - 1].Sequence : null);
    }

    private static void ValidatePlayerId(Guid playerId)
    {
        if (playerId == Guid.Empty)
            throw new InventoryValidationException(new() { ["playerId"] = "Jugador inválido." });
    }
}
