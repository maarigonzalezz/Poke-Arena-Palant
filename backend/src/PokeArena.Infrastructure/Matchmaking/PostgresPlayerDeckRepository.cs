using Microsoft.EntityFrameworkCore;
using PokeArena.Application.Matchmaking;
using PokeArena.Infrastructure.Persistence;

namespace PokeArena.Infrastructure.Matchmaking;

public sealed class PostgresPlayerDeckRepository(PokeArenaDbContext db) : IPlayerDeckRepository
{
    /// <summary>Resuelve códigos del catálogo a ids solo cuando el jugador posee al menos una copia.</summary>
    public async Task<IReadOnlyList<long>> ReadOwnedCardIdsAsync(Guid playerId,
        IReadOnlyList<string> cardCodes, CancellationToken token)
    {
        var owned = await (
            from card in db.Cards.AsNoTracking()
            join playerCard in db.PlayerCards.AsNoTracking() on card.Id equals playerCard.CardId
            where playerCard.PlayerId == playerId && playerCard.Copies > 0 && cardCodes.Contains(card.Code)
            select new { card.Code, card.Id })
            .ToListAsync(token);
        var byCode = owned.ToDictionary(x => x.Code, x => x.Id, StringComparer.Ordinal);
        return cardCodes.Where(byCode.ContainsKey).Select(code => byCode[code]).ToArray();
    }

    /// <summary>Persiste el snapshot del deck y devuelve la fecha de creación registrada en UTC.</summary>
    public async Task<DateTime> SaveAsync(string id, Guid playerId, string name,
        IReadOnlyList<long> cardIds, CancellationToken token)
    {
        var createdAt = DateTime.UtcNow;
        db.PlayerDecks.Add(new PlayerDeckRecord
        {
            Id = id,
            PlayerId = playerId,
            Name = name,
            CardIds = cardIds.ToArray(),
            CreatedAt = createdAt
        });
        await db.SaveChangesAsync(token);
        return createdAt;
    }
}
