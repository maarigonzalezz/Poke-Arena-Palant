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
        deckId = NormalizeDeckId(deckId);
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
            if (existing is not null && !ExpireIfInactive(existing))
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
            var deck = await decks.FindAsync(playerId, deckId, ct)
                ?? throw new MatchmakingFailure("deck_not_found", "El deck indicado no existe.", 404);
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
            search.LastPolledAt = search.CreatedAt;
            await session.AddSearchAsync(search, ct);
            return Result(search);
        }, token);
    }

    public async Task<MatchSearchResult?> CurrentAsync(Guid playerId, CancellationToken token)
    {
        ValidateIdentity(playerId);
        return await repository.InTransactionAsync(async (session, ct) =>
        {
            PlayerAccess.RequireActive(await session.LockPlayerAsync(playerId, ct));
            if (await session.FindOpenSearchAsync(playerId, ct) is { } open)
            {
                // Una consulta tardía informa la expiración y nunca revive la búsqueda anterior.
                if (!ExpireIfInactive(open)) open.LastPolledAt = clock.GetUtcNow().UtcDateTime;
                return Result(open);
            }
            var last = await repository.ReadCurrentAsync(playerId, ct);
            if (last is null || (last.Status == MatchmakingRules.Matched && !await session.HasActiveMatchAsync(playerId, ct)))
                return null;
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
                throw new MatchmakingFailure("match_already_created", $"Ya tiene asignada la partida {search.MatchId}; no puede cancelar la búsqueda.", 409,
                    new { search.MatchId });
            if (!ExpireIfInactive(search) && search.Status == MatchmakingRules.Searching)
                search.Status = MatchmakingRules.Cancelled;
            return Result(search);
        }, token);
    }

    public async Task<MatchSearchResult> AcceptBotAsync(Guid playerId, Guid searchId, CancellationToken token)
    {
        ValidateIdentity(playerId);
        var result = await repository.InTransactionAsync(async (session, ct) =>
        {
            var player = PlayerAccess.RequireActive(await session.LockPlayerAsync(playerId, ct));
            var search = await session.FindSearchAsync(searchId, playerId, ct)
                ?? throw new MatchmakingFailure("search_not_found", "La búsqueda no existe para este jugador.", 404);
            // Un reintento o un rival que llegó primero devuelve la partida ya asignada.
            if (search.Status == MatchmakingRules.Matched) return Result(search);
            if (ExpireIfInactive(search) || search.Status != MatchmakingRules.Searching) return Result(search);
            if (!MatchmakingRules.CanOfferBot(search, clock.GetUtcNow().UtcDateTime))
                throw new MatchmakingFailure("bot_not_available", $"La oferta del bot solo está disponible tras {MatchmakingRules.BotOfferAfterSeconds} segundos de búsqueda clasificatoria.", 409);
            if (!await CanContinueAsync(search, player, session, ct)) return Result(search);

            var match = new GameMatch {
                Id = Guid.NewGuid(), Context = search.Context, CreatedAt = clock.GetUtcNow().UtcDateTime,
                IsBot = true, BotCardIds = search.CardIds.ToArray()
            };
            session.AddMatch(match, Participant(search, match.Id));
            search.Status = MatchmakingRules.Matched;
            search.MatchId = match.Id;
            return Result(search);
        }, token);
        // La expiración debe quedar confirmada aunque la aceptación se rechace.
        if (result.Status != MatchmakingRules.Matched)
            throw new MatchmakingFailure(result.Status == MatchmakingRules.Expired ? "search_expired" : "search_closed",
                result.Message ?? "La búsqueda ya no está abierta.", 409, new { searchId, result.Status });
        return result;
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
            var cutoff = clock.GetUtcNow().UtcDateTime.AddSeconds(-MatchmakingRules.SearchInactivitySeconds);
            foreach (var inactive in await session.ReadInactiveAsync(cutoff, ct)) ExpireIfInactive(inactive);
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
                var candidates = await session.ReadCandidatesAsync(first,
                    MatchmakingRules.EloRange(first.CreatedAt, clock.GetUtcNow().UtcDateTime), ct);
                foreach (var second in candidates)
                {
                    if (second.Status != MatchmakingRules.Searching) continue;
                    var secondPlayer = await session.LockPlayerAsync(second.PlayerId, ct);
                    if (!await CanContinueAsync(second, secondPlayer, session, ct)) continue;
                    var now = clock.GetUtcNow().UtcDateTime;
                    if (ExpireIfInactive(first) || !MatchmakingRules.CanPair(first, second, now)) continue;
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
        deckId = NormalizeDeckId(deckId);
        return repository.InTransactionAsync(async (session, ct) =>
        {
            PlayerAccess.RequireActive(await session.LockPlayerAsync(playerId, ct));
            if (await session.IsDeckReservedAsync(playerId, deckId, ct))
                throw new MatchmakingFailure("deck_locked", "El deck está reservado por una búsqueda o partida activa.", 409);
            return await edit(ct);
        }, token);
    }

    private async Task<bool> CanContinueAsync(MatchSearch search, Domain.Entities.Player? player,
        IMatchmakingSession session, CancellationToken token)
    {
        if (ExpireIfInactive(search) || search.Status != MatchmakingRules.Searching) return false;
        if (player is null || !player.Active || (search.Context == MatchmakingRules.Ranked && player.Cubes < MatchmakingRules.RequiredCubes)
            || await session.HasActiveMatchAsync(search.PlayerId, token))
        {
            search.Status = MatchmakingRules.Cancelled;
            search.EndReason = "La búsqueda terminó porque la cuenta ya no cumple las condiciones para jugar.";
            return false;
        }
        return true;
    }

    private bool ExpireIfInactive(MatchSearch search)
    {
        if (!MatchmakingRules.IsExpired(search, clock.GetUtcNow().UtcDateTime)) return false;
        search.Status = MatchmakingRules.Expired;
        search.EndReason = $"La búsqueda expiró tras {MatchmakingRules.SearchInactivitySeconds} segundos sin consultas. Inicie otra búsqueda.";
        return true;
    }

    private static MatchPlayer Participant(MatchSearch search, Guid matchId) => new() {
        MatchId = matchId, PlayerId = search.PlayerId, DeckId = search.DeckId, CardIds = search.CardIds.ToArray(), Elo = search.Elo
    };

    private MatchSearchResult Result(MatchSearch search) => new(search.Id, search.Status, search.Context, search.ContextId,
        search.DeckId, search.Elo, MatchmakingRules.EloRange(search.CreatedAt, clock.GetUtcNow().UtcDateTime),
        search.CreatedAt, search.MatchId, search.EndReason, MatchmakingRules.CanOfferBot(search, clock.GetUtcNow().UtcDateTime));

    // Usa el mismo UUID para consultar mazos, reconocer reintentos y comprobar reservas.
    private static string NormalizeDeckId(string deckId) => Guid.TryParse(deckId, out var id) ? id.ToString() : deckId;

    private static void ValidateIdentity(Guid playerId)
    {
        if (playerId == Guid.Empty) throw new AuthenticationFailure("authentication_required", "Debe iniciar sesión para continuar.", 401);
    }
}
