using PokeArena.Application.Authentication;
using PokeArena.Domain.Catalogs;

namespace PokeArena.Application.Matchmaking;

public sealed record CreatePlayerDeckRequest(string Name, IReadOnlyList<string> CardCodes);
public sealed record PlayerDeckResult(string Id, string Name, IReadOnlyList<string> CardCodes, DateTime CreatedAt);

public interface IPlayerDeckRepository
{
    Task<IReadOnlyList<long>> ReadOwnedCardIdsAsync(Guid playerId, IReadOnlyList<string> cardCodes,
        CancellationToken cancellationToken);
    Task<DateTime> SaveAsync(string id, Guid playerId, string name, IReadOnlyList<long> cardIds,
        CancellationToken cancellationToken);
}

public sealed class PlayerDeckService(IPlayerDeckRepository decks)
{
    public async Task<PlayerDeckResult> CreateAsync(Guid playerId, CreatePlayerDeckRequest request,
        CancellationToken cancellationToken)
    {
        if (playerId == Guid.Empty)
            throw new AuthenticationFailure("authentication_required", "Debe iniciar sesión para continuar.", 401);
        if (request is null)
            throw new MatchmakingFailure("invalid_deck", "Debe proporcionar los datos del mazo.", 400);
        var name = request.Name?.Trim() ?? "";
        var codes = request.CardCodes ?? [];
        if (name.Length is < 1 or > 80)
            throw new MatchmakingFailure("invalid_deck_name", "El nombre del mazo debe tener entre 1 y 80 caracteres.", 400);
        if (codes.Count != GameRules.DeckSize || codes.Any(string.IsNullOrWhiteSpace)
            || codes.Distinct(StringComparer.Ordinal).Count() != GameRules.DeckSize)
            throw new MatchmakingFailure("invalid_deck", $"El mazo debe tener exactamente {GameRules.DeckSize} cartas distintas.", 400);

        var cardIds = await decks.ReadOwnedCardIdsAsync(playerId, codes, cancellationToken);
        if (cardIds.Count != GameRules.DeckSize)
            throw new MatchmakingFailure("deck_cards_not_owned",
                "Todas las cartas del mazo deben existir y pertenecer a su colección.", 400);

        var id = Guid.NewGuid().ToString("N");
        var createdAt = await decks.SaveAsync(id, playerId, name, cardIds, cancellationToken);
        return new PlayerDeckResult(id, name, codes.ToArray(), createdAt);
    }
}
