using Microsoft.EntityFrameworkCore;
using Npgsql;
using PokeArena.Application.Authentication;
using PokeArena.Domain.Entities;
using PokeArena.Infrastructure.Persistence;

namespace PokeArena.Infrastructure.Authentication;

/// <summary>Crea una sola cuenta por proveedor y sub, incluso con ingresos simultáneos.</summary>
public sealed class PostgresPlayerRepository(PokeArenaDbContext db) : IPlayerRepository
{
    public Task<Player?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Players.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<PlayerRegistration> GetOrCreateAsync(ExternalIdentity identity, CancellationToken cancellationToken)
    {
        var player = await FindIdentityAsync(identity, cancellationToken);
        if (player is null)
        {
            player = Player.Create(identity.Provider, identity.Subject, identity.Email, identity.DisplayName);
            db.Players.Add(player);
            try
            {
                // SaveChanges confirma el alta como una sola unidad transaccional.
                await db.SaveChangesAsync(cancellationToken);
                return new PlayerRegistration(player, true);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "uq_players_identity" })
            {
                // Otro ingreso ganó la carrera; recupera su cuenta sin duplicarla ni reiniciar Elo.
                db.Entry(player).State = EntityState.Detached;
                var existing = await FindIdentityAsync(identity, cancellationToken);
                if (existing is null) throw;
                player = existing;
            }
        }
        player.RecordLogin(identity.Email, identity.DisplayName);
        await db.SaveChangesAsync(cancellationToken);
        return new PlayerRegistration(player, false);
    }

    private Task<Player?> FindIdentityAsync(ExternalIdentity identity, CancellationToken cancellationToken) =>
        db.Players.SingleOrDefaultAsync(x => x.IdentityProvider == identity.Provider
            && x.ProviderSubject == identity.Subject, cancellationToken);
}
