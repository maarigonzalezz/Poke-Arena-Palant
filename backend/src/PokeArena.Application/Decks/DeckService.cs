using PokeArena.Application.Authentication;
using PokeArena.Domain.Catalogs;
using PokeArena.Domain.Decks;

namespace PokeArena.Application.Decks;

public sealed class DeckService(IDeckRepository decks, IPlayerRepository players, TimeProvider clock)
{
    public async Task<IReadOnlyList<DeckResult>> ListAsync(Guid playerId, CancellationToken token)
    {
        PlayerAccess.RequireActive(await players.FindAsync(playerId, token));
        return await decks.ListAsync(playerId, token);
    }

    public async Task<DeckResult> FindAsync(Guid playerId, Guid deckId, CancellationToken token)
    {
        PlayerAccess.RequireActive(await players.FindAsync(playerId, token));
        return await decks.FindAsync(playerId, deckId, token)
            ?? throw new DeckFailure("deck_not_found", "El mazo indicado no existe.", 404);
    }

    public async Task<DeckResult> CreateAsync(Guid playerId, string? key, CreateDeckRequest request, CancellationToken token)
    {
        PlayerAccess.RequireActive(await players.FindAsync(playerId, token));
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100 || key.Contains('\0'))
            throw new DeckFailure("invalid_idempotency_key", "Idempotency-Key es obligatorio y admite hasta 100 caracteres.", 400);

        var name = request.Name?.Trim();
        if (!PlayerDeck.IsValidName(name))
            throw new DeckFailure("invalid_deck_name",
                $"El nombre del mazo debe tener entre 1 y {PlayerDeck.MaximumNameLength} caracteres.", 400);

        var count = request.CardCodes?.Count ?? 0;
        if (count != GameRules.DeckSize)
            throw new DeckFailure("invalid_deck_size",
                $"El mazo debe tener {GameRules.DeckSize} cartas. Recibimos {count}.", 400,
                new { expectedCount = GameRules.DeckSize, actualCount = count });

        var codes = request.CardCodes!.Select(code => code?.Trim() ?? "").Order(StringComparer.Ordinal).ToArray();
        if (codes.Any(code => code.Length == 0 || code.Contains('\0')))
            throw new DeckFailure("invalid_deck_card_code", "Cada carta debe tener un código válido.", 400);

        var duplicates = codes.GroupBy(code => code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        if (duplicates.Length > 0)
            throw new DeckFailure("duplicate_deck_cards", $"Cartas repetidas: {string.Join(", ", duplicates)}.", 400,
                new { cardCodes = duplicates });

        return await decks.CreateOnceAsync(playerId, key, name, codes, async (session, ct) =>
        {
            var ownedCards = await session.ReadOwnedCardsAsync(codes, ct);
            var ownedByCode = ownedCards.ToDictionary(card => card.Code, StringComparer.Ordinal);
            var missing = codes.Where(code => !ownedByCode.ContainsKey(code)).ToArray();
            if (missing.Length > 0)
                throw new DeckFailure("deck_cards_not_owned", $"Estas cartas no están en tu colección: {string.Join(", ", missing)}.", 400,
                    new { cardCodes = missing });

            // Conserva la misma marca de tiempo en la respuesta de creación y en las consultas.
            var createdAt = DateTimeOffset.FromUnixTimeMilliseconds(clock.GetUtcNow().ToUnixTimeMilliseconds()).UtcDateTime;
            var deck = new PlayerDeck(playerId, name, createdAt,
                codes.Select(code => ownedByCode[code].CardId).ToArray());
            session.Add(deck);
            return new DeckResult(deck.Id, deck.Name, codes, deck.CreatedAt);
        }, token);
    }
}
