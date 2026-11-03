using Microsoft.EntityFrameworkCore;
using PokeArena.Application.Cards;
using PokeArena.Application.Common;
using PokeArena.Application.Locations;
using PokeArena.Infrastructure.Cards;
using PokeArena.Infrastructure.Locations;
using PokeArena.Infrastructure.Persistence;

namespace PokeArena.Infrastructure;

/// <summary>Comparte el mismo DbContext y su transacción entre los repositorios de escritura.</summary>
internal sealed class PostgresCatalogWriteSession(PokeArenaDbContext db) : ICatalogWriteSession
{
    public ICardCreateRepository Cards { get; } = new PostgresCardCreateRepository(db);
    public ILocationRepository Locations { get; } = new PostgresLocationRepository(db);

    public async Task<AbilityItem?> FindActiveAbilityAsync(string code, CancellationToken cancellationToken)
    {
        // EF materializa la habilidad; FOR SHARE conserva el bloqueo hasta confirmar la operación.
        var ability = await db.Abilities.FromSql($"""
            SELECT * FROM abilities WHERE code={code} AND active=TRUE FOR SHARE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return ability is null ? null : PostgresAbilityRepository.ToItem(ability);
    }
}
