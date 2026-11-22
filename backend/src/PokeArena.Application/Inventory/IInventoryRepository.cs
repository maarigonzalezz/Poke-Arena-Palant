using PokeArena.Application.Cards;
using PokeArena.Domain.Entities;

namespace PokeArena.Application.Inventory;

// Datos consultados bajo el bloqueo; las decisiones corresponden al servicio.
public sealed record InventoryState(Player? Player, bool Delivered, bool HasCards, bool HasMovements);
public sealed record InventorySnapshot(InventoryState State, IReadOnlyList<CardItem> Cards, IReadOnlyList<long> ActiveCardIds);
public sealed record InitialCollectionGrant(IReadOnlyList<long> CardIds, int Cubes, int Sequence, string Reason);

public interface IInventoryWriteSession
{
    Task<InventoryState> ReadStateAsync(CancellationToken cancellationToken);
    Task<List<long>> ReadCardIdsAsync(string rarity, bool active, CancellationToken cancellationToken);
    Task SaveGrantAsync(InitialCollectionGrant grant, CancellationToken cancellationToken);
}

public interface IInventoryRepository
{
    Task<InventorySnapshot> ReadAsync(Guid playerId, CancellationToken cancellationToken);
    Task<Player?> FindPlayerAsync(Guid playerId, CancellationToken cancellationToken);
    // La transacción y FOR UPDATE permanecen abiertos durante la decisión de Application.
    Task<T> WithPlayerLockAsync<T>(Guid playerId,
        Func<IInventoryWriteSession, CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
    Task<IReadOnlyList<CubeMovementItem>> ReadHistoryAsync(Guid playerId, int after, int take, CancellationToken cancellationToken);
}

// El adaptador traduce el fallo técnico; el servicio decide si la entrega puede reintentarse.
public sealed class InventoryPersistenceException(Exception inner) : Exception("No se pudo persistir el inventario.", inner);
