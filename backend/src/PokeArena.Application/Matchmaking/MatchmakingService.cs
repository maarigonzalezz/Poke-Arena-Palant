using PokeArena.Application.Authentication;
using PokeArena.Domain.Catalogs;
using PokeArena.Domain.Matchmaking;

namespace PokeArena.Application.Matchmaking;

// Todas las decisiones de admisión y selección de rival pertenecen a Application.
public sealed class MatchmakingService(IMatchmakingRepository repository, IMatchmakingDeckReader decks, TimeProvider clock)
    : IMatchmakingDeckGuard
{
    public Task<MatchSearchResult> StartRankedAsync(Guid playerId, string? deckId, CancellationToken token) =>
        StartAsync(playerId, deckId, MatchmakingRules.Ranked, null, token);

    // El contexto compartido permite que torneos use la misma reserva de búsqueda.
    public async Task<MatchSearchResult> StartAsync(Guid playerId, string? deckId, string context, Guid? contextId, CancellationToken token)
    {
        ValidateIdentity(playerId);
        if (string.IsNullOrWhiteSpace(deckId))
            throw new MatchmakingFailure("deck_required", "Debe seleccionar uno de sus decks.", 400);
        if (deckId.Length > MatchmakingRules.MaximumDeckIdLength)
            throw new MatchmakingFailure("invalid_deck_id", "El identificador del deck es inválido.", 400);
        if (context is not MatchmakingRules.Ranked and not MatchmakingRules.Tournament
            || (context == MatchmakingRules.Ranked && contextId is not null)
            || (context == MatchmakingRules.Tournament && (contextId is null || contextId == Guid.Empty)))
            throw new MatchmakingFailure("invalid_search_context", "El contexto de búsqueda es inválido.", 400);

        return await repository.InTransactionAsync(async (session, ct) =>
        {
            var player = PlayerAccess.RequireActive(await session.LockPlayerAsync(playerId, ct));
            if (await session.HasActiveMatchAsync(playerId, ct))
                throw new MatchmakingFailure("active_match", "Ya tiene una partida activa; no puede buscar otra.", 409);
            var existing = await session.FindOpenSearchAsync(playerId, ct);
            if (existing is not null)
            {
                if (existing.Context != context || existing.ContextId != contextId)
                    throw new MatchmakingFailure("search_already_open",
                        $"Ya tiene una búsqueda {(existing.Context == MatchmakingRules.Tournament ? "de torneo" : "clasificatoria")} abierta. Salga de ella antes de iniciar otra.", 409,
                        new { searchId = existing.Id, existing.Context, existing.ContextId });
                if (existing.DeckId != deckId)
                    throw new MatchmakingFailure("deck_locked", "No puede cambiar el deck mientras busca; salga de la búsqueda e inténtelo nuevamente.", 409);
                return Result(existing);
            }
            if (context == MatchmakingRules.Ranked && player.Cubes < MatchmakingRules.RequiredCubes)
                throw new MatchmakingFailure("insufficient_cubes",
                    $"Su saldo es de {player.Cubes} cubos. Necesita al menos {MatchmakingRules.RequiredCubes}; debe comprar cubos para continuar.", 400,
                    new { balance = player.Cubes, required = MatchmakingRules.RequiredCubes });
            var deck = await decks.FindAsync(deckId, ct)
                ?? throw new MatchmakingFailure("deck_not_found", "El deck indicado no existe.", 400);
            if (deck.OwnerId != playerId)
                throw new MatchmakingFailure("deck_not_owned", "El deck indicado pertenece a otro jugador.", 403);
            if (deck.CardIds.Count != GameRules.DeckSize || deck.CardIds.Distinct().Count() != GameRules.DeckSize)
                throw new MatchmakingFailure("invalid_deck", $"El deck debe tener exactamente {GameRules.DeckSize} cartas distintas.", 400);
            var owned = (await session.ReadOwnedCardIdsAsync(playerId, ct)).ToHashSet();
            if (deck.CardIds.Any(id => !owned.Contains(id)))
                throw new MatchmakingFailure("deck_cards_not_owned", "Todas las cartas del deck deben pertenecer a su colección.", 400);

            var search = new MatchSearch {
                Id = Guid.NewGuid(), PlayerId = playerId, DeckId = deck.Id, CardIds = deck.CardIds.ToArray(),
                Context = context, ContextId = contextId, Elo = player.Elo, CreatedAt = clock.GetUtcNow().UtcDateTime
            };
            search.LastCheckedAt = search.CreatedAt;
            session.AddSearch(search);
            return Result(search);
        }, token);
    }

    public async Task<MatchSearchResult?> CurrentAsync(Guid playerId, CancellationToken token)
    {
        ValidateIdentity(playerId);
        return await repository.InTransactionAsync(async (session, ct) =>
        {
            PlayerAccess.RequireActive(await session.LockPlayerAsync(playerId, ct));
            if (await session.FindOpenSearchAsync(playerId, ct) is { } open) return Result(open);
            var last = await repository.ReadCurrentAsync(playerId, ct);
            if (last is null || (last.Status == MatchmakingRules.Matched && !await session.HasActiveMatchAsync(playerId, ct)))
                return null;
            if (last.Context != MatchmakingRules.Tournament 
                && last.Status == MatchmakingRules.Searching
                && (clock.GetUtcNow().UtcDateTime - last.CreatedAt).TotalSeconds >= 60
            ) last.BotAvailable = true;
            return Result(last);
        }, token);
    }

    public async Task<MatchSearchResult> CancelAsync(Guid playerId, Guid searchId, CancellationToken token)
    {
        ValidateIdentity(playerId);
        return await repository.InTransactionAsync(async (session, ct) =>
        {
            PlayerAccess.RequireActive(await session.LockPlayerAsync(playerId, ct));
            var search = await session.FindSearchAsync(searchId, playerId, ct)
                ?? throw new MatchmakingFailure("search_not_found", "La búsqueda no existe para este jugador.", 404);
            if (search.Status == MatchmakingRules.Matched)
                throw new MatchmakingFailure("match_already_created", "La partida ya fue creada; no puede cancelar la búsqueda.", 409,
                    new { search.MatchId });
            search.Status = MatchmakingRules.Cancelled;
            return Result(search);
        }, token);
    }

    public async Task<IReadOnlyList<MatchmakingDeckOption>> DecksAsync(Guid playerId, CancellationToken token)
    {
        ValidateIdentity(playerId);
        return await repository.InTransactionAsync(async (session, ct) =>
        {
            PlayerAccess.RequireActive(await session.LockPlayerAsync(playerId, ct));
            return await decks.ListAsync(playerId, ct);
        }, token);
    }

    public Task<int> ProcessAsync(CancellationToken token) =>
        repository.InTransactionAsync(async (session, ct) =>
        {
            // Torneos comparte la reserva, pero su emparejamiento pertenece a su propio módulo.
            var waiting = await session.ReadWaitingAsync(MatchmakingRules.Ranked, ct);
            var created = 0;
            foreach (var first in waiting)
            {
                if (first.Status != MatchmakingRules.Searching) continue;
                // Rota los lotes sin reiniciar el tiempo usado para ampliar el Elo.
                first.LastCheckedAt = clock.GetUtcNow().UtcDateTime;
                var firstPlayer = await session.LockPlayerAsync(first.PlayerId, ct);
                if (!await CanContinueAsync(first, firstPlayer, session, ct)) continue;
                var now = clock.GetUtcNow().UtcDateTime;
                var candidates = await session.ReadCandidatesAsync(first, MatchmakingRules.EloRange(first.CreatedAt, now), ct);
                foreach (var second in candidates)
                {
                    if (second.Status != MatchmakingRules.Searching || !MatchmakingRules.CanPair(first, second, now)) continue;
                    var secondPlayer = await session.LockPlayerAsync(second.PlayerId, ct);
                    if (!await CanContinueAsync(second, secondPlayer, session, ct)) continue;
                    var match = new GameMatch { Id = Guid.NewGuid(), Context = first.Context, ContextId = first.ContextId, CreatedAt = now };
                    session.AddMatch(match, Participant(first, match.Id), Participant(second, match.Id));
                    first.Status = second.Status = MatchmakingRules.Matched;
                    first.MatchId = second.MatchId = match.Id;
                    created++;
                    break;
                }
            }
            return created;
        }, token);

    // Comparte el bloqueo con la admisión para que editar y entrar en cola no se crucen.
    public Task<T> WithUnlockedDeckAsync<T>(Guid playerId, string deckId,
        Func<CancellationToken, Task<T>> edit, CancellationToken token)
    {
        ValidateIdentity(playerId);
        return repository.InTransactionAsync(async (session, ct) =>
        {
            PlayerAccess.RequireActive(await session.LockPlayerAsync(playerId, ct));
            if (await session.IsDeckReservedAsync(playerId, deckId, ct))
                throw new MatchmakingFailure("deck_locked", "El deck está reservado por una búsqueda o partida activa.", 409);
            return await edit(ct);
        }, token);
    }

    private static async Task<bool> CanContinueAsync(MatchSearch search, Domain.Entities.Player? player,
        IMatchmakingSession session, CancellationToken token)
    {
        if (player is null || !player.Active || (search.Context == MatchmakingRules.Ranked && player.Cubes < MatchmakingRules.RequiredCubes)
            || await session.HasActiveMatchAsync(search.PlayerId, token))
        {
            search.Status = MatchmakingRules.Cancelled;
            search.EndReason = "La búsqueda terminó porque la cuenta ya no cumple las condiciones para jugar.";
            return false;
        }
        return true;
    }

    private static MatchPlayer Participant(MatchSearch search, Guid matchId) => new() {
        MatchId = matchId, PlayerId = search.PlayerId, DeckId = search.DeckId, CardIds = search.CardIds.ToArray(), Elo = search.Elo
    };

    private MatchSearchResult Result(MatchSearch search) => new(search.Id, search.Status, search.Context, search.ContextId,
        search.DeckId, search.Elo, MatchmakingRules.EloRange(search.CreatedAt, clock.GetUtcNow().UtcDateTime),
        search.CreatedAt, search.MatchId, search.EndReason, search.BotAvailable);

    private static void ValidateIdentity(Guid playerId)
    {
        if (playerId == Guid.Empty) throw new AuthenticationFailure("authentication_required", "Debe iniciar sesión para continuar.", 401);
    }
}
