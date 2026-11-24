using Microsoft.EntityFrameworkCore;
using Npgsql;
using PokeArena.Application.Decks;
using PokeArena.Domain.Decks;
using PokeArena.Infrastructure.Persistence;

namespace PokeArena.Infrastructure.Decks;

public sealed class PostgresDeckRepository(PokeArenaDbContext db) : IDeckRepository
{
    public Task<IReadOnlyList<DeckResult>> ListAsync(Guid playerId, CancellationToken token) =>
        ReadAsync(db.PlayerDecks.Where(deck => deck.PlayerId == playerId), token);

    public async Task<DeckResult?> FindAsync(Guid playerId, Guid deckId, CancellationToken token) =>
        (await ReadAsync(db.PlayerDecks.Where(deck => deck.PlayerId == playerId && deck.Id == deckId), token))
            .SingleOrDefault();

    public async Task<DeckResult> CreateOnceAsync(Guid playerId, string key, string name, string[] cardCodes,
        Func<IDeckWriteSession, CancellationToken, Task<DeckResult>> create, CancellationToken token)
    {
        var hash = IdempotentCreate.Fingerprint(new { name, cardCodes });
        try
        {
            return await IdempotentCreate.RunAsync(db, playerId.ToString(), key, "create-player-deck", hash,
                () => new DeckFailure("idempotency_key_conflict", "La clave de idempotencia ya se usó con otros datos.", 409),
                () => create(new WriteSession(db, playerId), token), token);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "uq_player_decks_name" })
        {
            throw new DeckFailure("deck_name_conflict", $"Ya tienes un mazo con el nombre «{name}».", 409, new { name });
        }
    }

    private async Task<IReadOnlyList<DeckResult>> ReadAsync(IQueryable<PlayerDeckRecord> decks, CancellationToken token)
    {
        var rows = await (
            from deck in decks
            join entry in db.PlayerDeckCards on deck.Id equals entry.DeckId
            join card in db.Cards on entry.CardId equals card.Id
            orderby deck.CreatedAt descending, deck.Id
            select new
            {
                deck.Id, deck.Name, deck.CreatedAt,
                Preview = new DeckCardPreview(card.Code, card.Name, card.CardType, card.Cost, card.Power,
                    card.ImageOriginalPath, card.ImageSmallPath)
            })
            .ToListAsync(token);
        return rows.GroupBy(row => new { row.Id, row.Name, row.CreatedAt })
            .Select(group =>
            {
                var cards = group.Select(row => row.Preview).OrderBy(card => card.Code, StringComparer.Ordinal).ToArray();
                return new DeckResult(group.Key.Id, group.Key.Name, cards.Select(card => card.Code).ToArray(), group.Key.CreatedAt)
                {
                    Cards = cards
                };
            })
            .ToArray();
    }

    private sealed class WriteSession(PokeArenaDbContext db, Guid playerId) : IDeckWriteSession
    {
        public async Task<IReadOnlyList<OwnedDeckCard>> ReadOwnedCardsAsync(string[] codes, CancellationToken token) =>
            // Mantiene la propiedad comprobada hasta confirmar el mazo.
            await db.Database.SqlQuery<OwnedDeckCard>($"""
                SELECT card.id AS "CardId", card.code AS "Code"
                FROM player_cards inventory JOIN cards card ON card.id = inventory.card_id
                WHERE inventory.player_id = {playerId} AND inventory.copies > 0 AND card.code = ANY({codes})
                ORDER BY inventory.card_id
                FOR SHARE OF inventory
                """).ToListAsync(token);

        public void Add(PlayerDeck deck)
        {
            // Usa la misma comparación de nombres con cualquier configuración regional de PostgreSQL.
            var nameKey = deck.Name.Normalize().ToUpperInvariant();
            db.PlayerDecks.Add(new(deck.Id, deck.PlayerId, deck.Name, nameKey, deck.CreatedAt));
            db.PlayerDeckCards.AddRange(deck.CardIds.Select(cardId => new PlayerDeckCardRecord(deck.Id, cardId)));
        }
    }
}
