using Microsoft.EntityFrameworkCore;
using Npgsql;
using PokeArena.Application.Matchmaking;
using PokeArena.Infrastructure.Persistence;

namespace PokeArena.Infrastructure.Matchmaking;

// El módulo de mazos publica esta vista; aquí no se crean ni se simulan decks.
public sealed class PostgresMatchmakingDeckReader(PokeArenaDbContext db) : IMatchmakingDeckReader
{
    /// <summary>Lee todas las cartas del deck y su propietario desde la vista publicada por el módulo de mazos.</summary>
    public async Task<MatchmakingDeck?> FindAsync(string deckId, CancellationToken token)
    {
        try
        {
            var rows = await db.MatchmakingDeckRows.AsNoTracking().Where(x => x.DeckId == deckId).ToListAsync(token);
            return rows.Count == 0 ? null : new(rows[0].DeckId, rows[0].PlayerId,
                rows.Where(x => x.CardId.HasValue).Select(x => x.CardId!.Value).ToArray());
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            throw NotIntegrated();
        }
    }

    /// <summary>Lista por nombre los decks publicados que pertenecen al jugador solicitado.</summary>
    public async Task<IReadOnlyList<MatchmakingDeckOption>> ListAsync(Guid playerId, CancellationToken token)
    {
        try
        {
            return await db.MatchmakingDeckRows.AsNoTracking().Where(x => x.PlayerId == playerId)
                // Ordena columnas traducibles a SQL antes de construir el contrato de respuesta.
                .Select(x => new { x.DeckId, x.Name }).Distinct().OrderBy(x => x.Name).ThenBy(x => x.DeckId)
                .Select(x => new MatchmakingDeckOption(x.DeckId, x.Name)).ToListAsync(token);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            throw NotIntegrated();
        }
    }

    /// <summary>Devuelve el error explícito que permite distinguir una integración ausente de un mazo inexistente.</summary>
    private static MatchmakingFailure NotIntegrated() => new("decks_not_integrated",
        "El módulo de mazos todavía no está conectado. Debe publicar la vista de consulta para poder seleccionar un deck.", 503);
}
