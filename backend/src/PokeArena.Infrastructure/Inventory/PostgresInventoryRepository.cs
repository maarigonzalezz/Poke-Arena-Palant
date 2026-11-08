using Microsoft.EntityFrameworkCore;
using Npgsql;

using PokeArena.Domain.Entities;
using PokeArena.Application.Inventory;
using PokeArena.Infrastructure.Persistence;

namespace PokeArena.Infrastructure.Inventory;

public sealed class PostgresInventoryRepository(PokeArenaDbContext db) : IInventoryRepository
{   
    /// <summary>Consultar el inventario de un jugador. Si no tiene inventario, genera uno automaticamente.</summary>
    public async Task<PlayerProfileResult> ConsultAsync(
        PlayerProfileQuery query,
        CancellationToken cancellationToken = default
    ){
        // Buscar jugador
        var player = await db.Players
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == query.Profile.Id, cancellationToken);

        // Verificar que el jugador existe
        if (player is null)
            throw new KeyNotFoundException("Player not found");

        var inventory = new PlayerInventory(
            new PlayerWallet([]),
            new PlayerCollection([])
        );

        bool stil_new_player = false;

        return new PlayerProfileResult(
            query.Profile, 
            stil_new_player, 
            inventory
        );
    }
}