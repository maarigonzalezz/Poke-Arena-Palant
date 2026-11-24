using PokeArena.Application.Authentication;
using System.Buffers.Binary;
using System.Text.Json;
using PokeArena.Domain.Catalogs;
using PokeArena.Domain.Entities;
using PokeArena.Domain.Matchmaking;

namespace PokeArena.Application.Matchmaking;

// Todas las decisiones de admisión y selección de rival pertenecen a Application.
public sealed class MatchmakingService(IMatchmakingRepository repository, IMatchmakingDeckReader decks, TimeProvider clock)
    : IMatchmakingDeckGuard
{
    /// <summary>Inicia una búsqueda clasificatoria después de validar la cuenta y el mazo del jugador.</summary>
    public Task<MatchSearchResult> StartRankedAsync(Guid playerId, string? deckId, CancellationToken token) =>
        StartAsync(playerId, deckId, MatchmakingRules.Ranked, null, token);

    // El contexto compartido permite que torneos use la misma reserva de búsqueda.
    /// <summary>
    /// Valida y guarda una copia del mazo seleccionado, y reserva al jugador en una cola clasificatoria o de torneo.
    /// </summary>
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

    /// <summary>Devuelve la búsqueda actual y renueva la presencia solo mientras siga abierta.</summary>
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
            if (last is null) return null;
            return Result(last);
        }, token);
    }

    /// <summary>Cancela una búsqueda abierta del jugador; una partida ya creada no se puede cancelar aquí.</summary>
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

    /// <summary>Acepta una oferta de bot clasificatoria disponible y prepara la partida con las reglas compartidas.</summary>
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
            try
            {
                var participant = Participant(search, match.Id);
                await PrepareMatchAsync(match, [participant], session, ct);
                session.AddMatch(match, participant);
                search.Status = MatchmakingRules.Matched;
                search.MatchId = match.Id;
            }
            catch (MatchmakingFailure failure)
            {
                if (failure.Code == "locations_unavailable")
                    search.EndReason = failure.Message;
                else
                    throw;
            }
            return Result(search);
        }, token);
        // La expiración debe quedar confirmada aunque la aceptación se rechace.
        if (result.Status != MatchmakingRules.Matched)
            throw new MatchmakingFailure(result.Status == MatchmakingRules.Expired ? "search_expired" : "search_closed",
                result.Message ?? "La búsqueda ya no está abierta.", 409, new { searchId, result.Status });
        return result;
    }

    /// <summary>Lista únicamente los mazos del jugador autenticado disponibles para matchmaking.</summary>
    public async Task<IReadOnlyList<MatchmakingDeckOption>> DecksAsync(Guid playerId, CancellationToken token)
    {
        ValidateIdentity(playerId);
        return await repository.InTransactionAsync(async (session, ct) =>
        {
            PlayerAccess.RequireActive(await session.LockPlayerAsync(playerId, ct));
            return await decks.ListAsync(playerId, ct);
        }, token);
    }

    /// <summary>Construye el estado visible para el participante sin exponer la mano ni el orden del mazo rival.</summary>
    public async Task<MatchStateResult?> MatchStateAsync(Guid playerId, Guid matchId, CancellationToken token)
    {
        ValidateIdentity(playerId);
        return await repository.InTransactionAsync(async (session, ct) =>
        {
            PlayerAccess.RequireActive(await session.LockPlayerAsync(playerId, ct));
            var match = await session.LockMatchAsync(matchId, playerId, ct);
            if (match is null) return null;
            var players = await session.ReadMatchPlayersAsync(matchId, ct);
            var choices = await session.ReadMatchLocationChoicesAsync(matchId, ct);
            return await BuildMatchStateAsync(match, players, choices, playerId, session, ct);
        }, token);
    }

    /// <summary>
    /// Juega una carta de la mano tras validar el temporizador, los espacios y restricciones de la ubicación,
    /// la disponibilidad de la carta y su costo efectivo de energía.
    /// </summary>
    public async Task<MatchStateResult?> PlayCardAsync(Guid playerId, Guid matchId, long cardId,
        int locationSlot, CancellationToken token)
    {
        ValidateIdentity(playerId);
        return await repository.InTransactionAsync(async (session, ct) =>
        {
            PlayerAccess.RequireActive(await session.LockPlayerAsync(playerId, ct));
            var match = await session.LockMatchAsync(matchId, playerId, ct);
            if (match is null) return null;
            EnsureTurnIsPlayable(match);

            var players = await session.ReadMatchPlayersAsync(matchId, ct);
            var ownPlayer = players.Single(player => player.PlayerId == playerId);
            if (!ownPlayer.HandCardIds.Contains(cardId))
                throw new MatchmakingFailure("card_not_in_hand", "Esa carta no está en su mano.", 409);

            var choice = (await session.ReadMatchLocationChoicesAsync(matchId, ct))
                .SingleOrDefault(location => location.Slot == locationSlot);
            if (choice is null)
                throw new MatchmakingFailure("location_not_found", "La ubicación seleccionada no pertenece a esta partida.", 400);
            var location = ReadLocation(choice);
            var cardsAtLocation = ownPlayer.PlayedLocationSlots.Count(slot => slot == locationSlot);
            if (location.MaxCards <= cardsAtLocation)
                throw new MatchmakingFailure("location_full", "No quedan espacios para sus cartas en esta ubicación.", 409);
            if (IsTurnRestricted(location, match.TurnNumber))
                throw new MatchmakingFailure("location_restricted", "Esta ubicación no permite jugar cartas durante este turno.", 409);

            var card = (await session.ReadCardsAsync([cardId], ct)).SingleOrDefault();
            if (card is null || !card.Active)
                throw new MatchmakingFailure("card_unavailable", "La carta ya no está disponible en el catálogo.", 409);
            var cost = EffectiveCost(card, location);
            if (ownPlayer.Energy < cost)
                throw new MatchmakingFailure("insufficient_energy", $"Necesita {cost} de energía para jugar esta carta.", 409);

            ownPlayer.HandCardIds = ownPlayer.HandCardIds.Where(id => id != cardId).ToArray();
            ownPlayer.PlayedCardIds = ownPlayer.PlayedCardIds.Append(cardId).ToArray();
            ownPlayer.PlayedLocationSlots = ownPlayer.PlayedLocationSlots.Append(locationSlot).ToArray();
            ownPlayer.Energy -= cost;
            return await BuildMatchStateAsync(match, players,
                await session.ReadMatchLocationChoicesAsync(matchId, ct), playerId, session, ct);
        }, token);
    }

    /// <summary>Mueve una carta de la mano del jugador a su cementerio durante un turno activo.</summary>
    public async Task<MatchStateResult?> DiscardCardAsync(Guid playerId, Guid matchId, long cardId,
        CancellationToken token)
    {
        ValidateIdentity(playerId);
        return await repository.InTransactionAsync(async (session, ct) =>
        {
            PlayerAccess.RequireActive(await session.LockPlayerAsync(playerId, ct));
            var match = await session.LockMatchAsync(matchId, playerId, ct);
            if (match is null) return null;
            EnsureTurnIsPlayable(match);

            var players = await session.ReadMatchPlayersAsync(matchId, ct);
            var ownPlayer = players.Single(player => player.PlayerId == playerId);
            if (!ownPlayer.HandCardIds.Contains(cardId))
                throw new MatchmakingFailure("card_not_in_hand", "Esa carta no está en su mano.", 409);
            ownPlayer.HandCardIds = ownPlayer.HandCardIds.Where(id => id != cardId).ToArray();
            ownPlayer.CemeteryCardIds = ownPlayer.CemeteryCardIds.Append(cardId).ToArray();
            return await BuildMatchStateAsync(match, players,
                await session.ReadMatchLocationChoicesAsync(matchId, ct), playerId, session, ct);
        }, token);
    }

    /// <summary>
    /// Avanza idempotentemente un turno vencido, roba cartas y recalcula la energía, o resuelve la partida al terminar el turno seis.
    /// </summary>
    public async Task<MatchStateResult?> AdvanceMatchAsync(Guid playerId, Guid matchId, int? expectedTurnNumber,
        CancellationToken token)
    {
        ValidateIdentity(playerId);
        return await repository.InTransactionAsync(async (session, ct) =>
        {
            PlayerAccess.RequireActive(await session.LockPlayerAsync(playerId, ct));
            var match = await session.LockMatchAsync(matchId, playerId, ct);
            if (match is null) return null;
            if (expectedTurnNumber is { } expected && expected < match.TurnNumber)
                return await BuildMatchStateAsync(match, await session.ReadMatchPlayersAsync(matchId, ct),
                    await session.ReadMatchLocationChoicesAsync(matchId, ct), playerId, session, ct);
            if (expectedTurnNumber is { } futureTurn && futureTurn > match.TurnNumber)
                throw new MatchmakingFailure("stale_turn", "El turno solicitado no coincide con la partida actual.", 409);
            var now = clock.GetUtcNow().UtcDateTime;
            if (match.Status != MatchmakingRules.ActiveMatch)
                return await BuildMatchStateAsync(match, await session.ReadMatchPlayersAsync(matchId, ct),
                    await session.ReadMatchLocationChoicesAsync(matchId, ct), playerId, session, ct);
            if (match.TurnEndsAt is not null && now < match.TurnEndsAt.Value)
                throw new MatchmakingFailure("turn_not_finished",
                    $"El turno todavía tiene tiempo restante.", 409,
                    new { turnEndsAt = match.TurnEndsAt.Value });

            var players = await session.ReadMatchPlayersAsync(matchId, ct);
            if (match.TurnNumber >= 6)
            {
                var finalLocations = await session.ReadMatchLocationChoicesAsync(matchId, ct);
                var ownPlayer = players.Single(player => player.PlayerId == playerId);
                var opponentPlayer = players.SingleOrDefault(player => player.PlayerId != playerId);
                var result = await CalculateResultAsync(match, ownPlayer, opponentPlayer, finalLocations, session, ct);
                match.Status = MatchmakingRules.CompletedMatch;
                match.TurnEndsAt = null;
                match.IsDraw = result.Outcome == "Draw";
                match.WinnerPlayerId = result.Outcome switch
                {
                    "Player" => playerId,
                    "Opponent" => opponentPlayer?.PlayerId,
                    _ => null
                };
                await SettleWagerAsync(match, players, match.WinnerPlayerId, match.IsDraw.Value, session, ct);
                foreach (var player in players) player.Active = false;
                return await BuildMatchStateAsync(match, players,
                    finalLocations, playerId, session, ct);
            }

            match.TurnNumber++;
            match.TurnStartedAt = now;
            match.TurnEndsAt = now.AddSeconds(MatchmakingRules.TurnDurationSeconds);

            var choices = await session.ReadMatchLocationChoicesAsync(matchId, ct);
            foreach (var player in players)
            {
                if (MatchmakingRules.TryDrawCard(player.HandCardIds, player.RemainingCardIds,
                    out var nextHand, out var nextDeck))
                {
                    player.HandCardIds = nextHand;
                    player.RemainingCardIds = nextDeck;
                }
                player.Energy = TurnEnergy(match.TurnNumber, choices, player);
            }

            if (match.IsBot)
            {
                var botDeck = match.BotCardIds ?? [];
                match.BotHandCardIds = match.BotHandCardIds ?? [];
                var botDrawIndex = MatchmakingRules.InitialHandSize + (match.TurnNumber - 1);
                if (match.BotHandCardIds.Length < MatchmakingRules.MaximumHandSize
                    && botDrawIndex >= 0 && botDrawIndex < botDeck.Length)
                    match.BotHandCardIds = match.BotHandCardIds.Append(botDeck[botDrawIndex]).ToArray();
                match.BotEnergy = MatchmakingRules.BaseTurnEnergy(match.TurnNumber)
                    + choices.Sum(choice => ReadLocation(choice) is { EnergyMode: "Fixed" } location
                        ? location.ExtraEnergy : 0);
            }
            return await BuildMatchStateAsync(match, players, choices, playerId, session, ct);
        }, token);
    }

    // Ranked y Tournament comparten preparación. El match y el emparejamiento
    // se confirman juntos; un fallo no deja una partida parcial persistida.
    /// <summary>Empareja búsquedas elegibles y prepara partidas clasificatorias y de torneo en una transacción.</summary>
    public Task<int> ProcessAsync(CancellationToken token) =>
        repository.InTransactionAsync(async (session, ct) =>
        {
            var cutoff = clock.GetUtcNow().UtcDateTime.AddSeconds(-MatchmakingRules.SearchInactivitySeconds);
            foreach (var inactive in await session.ReadInactiveAsync(cutoff, ct)) ExpireIfInactive(inactive);
            var created = 0;
            foreach (var context in new[] { MatchmakingRules.Ranked, MatchmakingRules.Tournament })
            foreach (var first in await session.ReadWaitingAsync(context, ct))
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
                    var firstParticipant = Participant(first, match.Id);
                    var secondParticipant = Participant(second, match.Id);
                    try
                    {
                        await PrepareMatchAsync(match, [firstParticipant, secondParticipant], session, ct);
                        session.AddMatch(match, firstParticipant, secondParticipant);
                        first.Status = second.Status = MatchmakingRules.Matched;
                        first.MatchId = second.MatchId = match.Id;
                        created++;
                    }
                    catch (MatchmakingFailure failure)
                    {
                        if (failure.Code == "locations_unavailable")
                            first.EndReason = second.EndReason = failure.Message;
                        else
                            throw;
                    }
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

    /// <summary>Revalida la cuenta y cierra o expira búsquedas que ya no pueden continuar.</summary>
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

    /// <summary>Expira una búsqueda desatendida según la última consulta del cliente, no la actividad del worker.</summary>
    private bool ExpireIfInactive(MatchSearch search)
    {
        if (!MatchmakingRules.IsExpired(search, clock.GetUtcNow().UtcDateTime)) return false;
        search.Status = MatchmakingRules.Expired;
        search.EndReason = $"La búsqueda expiró tras {MatchmakingRules.SearchInactivitySeconds} segundos sin consultas. Inicie otra búsqueda.";
        return true;
    }

    // Persiste una instantánea de ubicaciones y mazos barajados, reparte tres cartas
    // y luego el robo del turno 1, inicializa energía, cementerios y reloj.
    /// <summary>
    /// Elige y guarda las ubicaciones, baraja cada mazo con semillas del servidor,
    /// reparte la mano inicial y el robo del turno uno e inicializa energía, apuesta, cementerios y temporizador.
    /// </summary>
    private async Task PrepareMatchAsync(GameMatch match, IReadOnlyList<MatchPlayer> participants,
        IMatchmakingSession session, CancellationToken token)
    {
        var locations = await session.ReadMatchLocationsAsync(token);

        match.PreparationSeed = MatchPreparationRules.CreateSeed();
        IReadOnlyList<PokeArena.Domain.Entities.Location> chosen;
        try
        {
            chosen = MatchPreparationRules.ChooseLocations(locations, match.PreparationSeed);
        }
        catch (InvalidOperationException)
        {
            throw new MatchmakingFailure("locations_unavailable",
                "La partida no se creó porque no existen 3 ubicaciones disponibles que cumplan las reglas.", 409);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        match.TurnNumber = MatchmakingRules.InitialTurn;
        match.TurnStartedAt = now;
        match.TurnEndsAt = now.AddSeconds(MatchmakingRules.TurnDurationSeconds);
        match.WagerCubes = match.Context == MatchmakingRules.Ranked ? MatchmakingRules.RankedWagerCubes : 0;
        match.Locations.Clear();

        for (var index = 0; index < chosen.Count; index++)
        {
            var location = chosen[index];
            match.Locations.Add(new MatchLocationChoice
            {
                MatchId = match.Id,
                Slot = index + 1,
                LocationId = location.Id,
                Snapshot = JsonSerializer.Serialize(location, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            });
        }

        foreach (var participant in participants)
        {
            var deckSeed = unchecked(match.PreparationSeed ^ PlayerSeed(participant.PlayerId));
            var shuffled = MatchPreparationRules.ShuffleDeck(participant.CardIds, deckSeed);
            participant.CardIds = shuffled.ToArray();
            var openingHand = MatchPreparationRules.OpeningHand(shuffled);
            MatchmakingRules.TryDrawCard(openingHand, shuffled.Skip(MatchmakingRules.InitialHandSize).ToArray(),
                out var firstTurnHand, out var firstTurnDeck);
            participant.HandCardIds = firstTurnHand;
            participant.RemainingCardIds = firstTurnDeck;
            participant.CemeteryCardIds = [];
            participant.Energy = InitialTurnEnergy(match.TurnNumber, chosen);
        }

        if (match.WagerCubes > 0)
        {
            foreach (var participant in participants)
            {
                var player = PlayerAccess.RequireActive(await session.LockPlayerAsync(participant.PlayerId, token));
                if (player.Cubes < match.WagerCubes)
                    throw new MatchmakingFailure("insufficient_cubes",
                        "Un jugador ya no tiene cubos suficientes para cubrir la apuesta.", 409);
                player.Cubes -= match.WagerCubes;
            }
        }

        if (match.IsBot && match.BotCardIds is not null)
        {
            var botDeck = MatchPreparationRules.ShuffleDeck(participants[0].CardIds,
                unchecked(match.PreparationSeed ^ 0x5A17C9E3));
            var botOpeningHand = MatchPreparationRules.OpeningHand(botDeck);
            MatchmakingRules.TryDrawCard(botOpeningHand, botDeck.Skip(MatchmakingRules.InitialHandSize).ToArray(),
                out var botTurnOneHand, out _);
            match.BotCardIds = botDeck.ToArray();
            match.BotHandCardIds = botTurnOneHand;
            match.BotCemeteryCardIds = [];
            match.BotEnergy = InitialTurnEnergy(match.TurnNumber, chosen);
        }
    }

    /// <summary>Obtiene un componente estable por jugador para que cada participante tenga un barajado reproducible distinto.</summary>
    private static long PlayerSeed(Guid playerId)
    {
        var bytes = playerId.ToByteArray();
        return BinaryPrimitives.ReadInt64LittleEndian(bytes);
    }

    /// <summary>Crea un participante desde la copia inmutable del mazo capturada al entrar a la cola.</summary>
    private static MatchPlayer Participant(MatchSearch search, Guid matchId) => new() {
        MatchId = matchId, PlayerId = search.PlayerId, DeckId = search.DeckId, CardIds = search.CardIds.ToArray(), Elo = search.Elo
    };

    /// <summary>Compara el poder en cada ubicación guardada y aplica los desempates por energía y cementerio.</summary>
    private static async Task<(string Outcome, int PlayerLocationsWon, int OpponentLocationsWon,
        int PlayerTotalPower, int OpponentTotalPower)> CalculateResultAsync(GameMatch match,
        MatchPlayer player, MatchPlayer? opponent, IReadOnlyList<MatchLocationChoice> choices,
        IMatchmakingSession session, CancellationToken token)
    {
        var opponentPlayedIds = opponent?.PlayedCardIds ?? [];
        var cardsById = (await session.ReadCardsAsync(
            player.PlayedCardIds.Concat(opponentPlayedIds).Distinct().ToArray(), token))
            .ToDictionary(card => card.Id);
        var playerPower = player.PlayedCardIds.Sum(id => cardsById[id].Power);
        var opponentPower = opponentPlayedIds.Sum(id => cardsById[id].Power);
        var playerLocationsWon = 0;
        var opponentLocationsWon = 0;
        foreach (var choice in choices)
        {
            var ownAtLocation = player.PlayedCardIds.Select((id, index) => (id, slot: player.PlayedLocationSlots[index]))
                .Where(card => card.slot == choice.Slot).Sum(card => cardsById[card.id].Power);
            var opponentAtLocation = opponent is null ? 0
                : opponentPlayedIds.Select((id, index) => (id, slot: opponent.PlayedLocationSlots[index]))
                    .Where(card => card.slot == choice.Slot).Sum(card => cardsById[card.id].Power);
            if (ownAtLocation > opponentAtLocation) playerLocationsWon++;
            else if (opponentAtLocation > ownAtLocation) opponentLocationsWon++;
        }

        var outcome = MatchmakingRules.MatchOutcome(playerLocationsWon, opponentLocationsWon,
            player.Energy, opponent?.Energy ?? match.BotEnergy,
            player.CemeteryCardIds.Length,
            opponent?.CemeteryCardIds.Length ?? match.BotCemeteryCardIds.Length);
        return (outcome, playerLocationsWon, opponentLocationsWon, playerPower, opponentPower);
    }

    /// <summary>Liquida la apuesta clasificatoria: el ganador recibe el pozo o, en empate, se devuelve a ambos.</summary>
    private static async Task SettleWagerAsync(GameMatch match, IReadOnlyList<MatchPlayer> players,
        Guid? winnerPlayerId, bool isDraw, IMatchmakingSession session, CancellationToken token)
    {
        if (match.Context != MatchmakingRules.Ranked || match.WagerCubes == 0) return;
        if (isDraw)
        {
            foreach (var player in players)
            {
                var account = PlayerAccess.RequireActive(await session.LockPlayerAsync(player.PlayerId, token));
                account.Cubes = checked(account.Cubes + match.WagerCubes);
            }
            return;
        }

        if (winnerPlayerId is { } winner)
        {
            var account = PlayerAccess.RequireActive(await session.LockPlayerAsync(winner, token));
            account.Cubes = checked(account.Cubes + match.WagerCubes * 2);
        }
    }

    /// <summary>Construye el snapshot para un participante, con datos públicos del rival y sin sus cartas ocultas.</summary>
    private async Task<MatchStateResult> BuildMatchStateAsync(GameMatch match, IReadOnlyList<MatchPlayer> players,
        IReadOnlyList<MatchLocationChoice> choices, Guid playerId, IMatchmakingSession session, CancellationToken token)
    {
        var locations = choices.Select(choice =>
        {
            var location = ReadLocation(choice);
            return new MatchLocationState(choice.Slot, location.Id, location.Name, location.Description, location.MaxCards,
                location.EnergyMode, location.ExtraEnergy, location.EffectType,
                ReadRestrictedTurns(location), location.ImageSmallPath);
        }).ToArray();
        var ownPlayer = players.Single(player => player.PlayerId == playerId);
        var opponentPlayer = players.SingleOrDefault(player => player.PlayerId != playerId);
        // Solo se serializa la mano propia; del oponente se expone el conteo, nunca sus cartas ni el orden del mazo.
        var opponentHandIds = opponentPlayer?.HandCardIds ?? match.BotHandCardIds ?? [];
        var opponentCemeteryIds = opponentPlayer?.CemeteryCardIds ?? match.BotCemeteryCardIds;
        var opponentPlayedIds = opponentPlayer?.PlayedCardIds ?? [];
        var cardIds = ownPlayer.HandCardIds.Concat(ownPlayer.PlayedCardIds)
            .Concat(ownPlayer.CemeteryCardIds).Concat(opponentPlayedIds)
            .Concat(opponentCemeteryIds).Distinct().ToArray();
        var cards = (await session.ReadCardsAsync(cardIds, token)).ToDictionary(card => card.Id);
        var hand = ownPlayer.HandCardIds.Select(id => ToMatchCard(cards, id)).ToArray();
        var cemetery = ownPlayer.CemeteryCardIds.Select(id => ToMatchCard(cards, id)).ToArray();
        var board = ownPlayer.PlayedCardIds.Select((id, index) =>
        {
            var card = ToMatchCard(cards, id);
            return new MatchBoardCardState(card.Id, card.Name, card.Cost, card.Power,
                ownPlayer.PlayedLocationSlots[index], card.ImagePath);
        }).ToArray();
        var playerState = new MatchPlayerState(ownPlayer.PlayerId, false, hand,
            ownPlayer.CemeteryCardIds, cemetery, board, ownPlayer.Energy, ownPlayer.RemainingCardIds.Length);
        var opponentBoard = opponentPlayer is null
            ? Array.Empty<MatchBoardCardState>()
            : opponentPlayer.PlayedCardIds.Select((id, index) =>
            {
                var card = ToMatchCard(cards, id);
                return new MatchBoardCardState(card.Id, card.Name, card.Cost, card.Power,
                    opponentPlayer.PlayedLocationSlots[index], card.ImagePath);
            }).ToArray();
        var opponentCemetery = opponentCemeteryIds.Select(id => ToMatchCard(cards, id)).ToArray();
        var opponentState = new MatchOpponentState(match.IsBot, opponentHandIds.Length,
            opponentCemetery, opponentBoard, opponentPlayer?.Energy ?? match.BotEnergy,
            opponentPlayer?.RemainingCardIds.Length ?? Math.Max(0, (match.BotCardIds?.Length ?? 0)
                - MatchmakingRules.InitialHandSize - match.TurnNumber));
        MatchResultState? result = null;
        if (match.Status == MatchmakingRules.CompletedMatch)
        {
            var playerPower = board.Sum(card => card.Power);
            var opponentPower = opponentBoard.Sum(card => card.Power);
            var playerLocationsWon = 0;
            var opponentLocationsWon = 0;
            foreach (var location in locations)
            {
                var playerAtLocation = board.Where(card => card.LocationSlot == location.Slot).Sum(card => card.Power);
                var opponentAtLocation = opponentBoard.Where(card => card.LocationSlot == location.Slot).Sum(card => card.Power);
                if (playerAtLocation > opponentAtLocation) playerLocationsWon++;
                else if (opponentAtLocation > playerAtLocation) opponentLocationsWon++;
            }
            var outcome = match.IsDraw is null ? "Completed"
                : match.IsDraw.Value ? "Draw"
                : match.WinnerPlayerId == playerId ? "Win" : "Loss";
            var cubesAwarded = match.Context != MatchmakingRules.Ranked ? 0
                : match.IsDraw == true ? match.WagerCubes
                : outcome == "Win" ? match.WagerCubes * 2 : 0;
            result = new MatchResultState(outcome, playerLocationsWon, opponentLocationsWon,
                playerPower, opponentPower, ownPlayer.Energy, opponentState.Energy,
                ownPlayer.CemeteryCardIds.Length, opponentCemeteryIds.Length, cubesAwarded);
        }
        return new MatchStateResult(match.Id, match.Context, match.Status, match.IsBot,
            match.TurnNumber, MatchmakingRules.TurnDurationSeconds, match.TurnStartedAt, match.TurnEndsAt,
            match.WagerCubes, locations, playerState, opponentState, result);
    }

    /// <summary>Rechaza acciones de cartas si la partida terminó o venció el turno actual.</summary>
    private void EnsureTurnIsPlayable(GameMatch match)
    {
        if (match.Status != MatchmakingRules.ActiveMatch)
            throw new MatchmakingFailure("match_completed", "La partida ya terminó.", 409);
        if (match.TurnEndsAt is null || clock.GetUtcNow().UtcDateTime >= match.TurnEndsAt.Value)
            throw new MatchmakingFailure("turn_finished", "El tiempo del turno terminó; espere el avance al siguiente turno.", 409);
    }

    /// <summary>Calcula la energía inicial sumando energía base y efectos de ubicaciones sobre un campo vacío.</summary>
    private static int InitialTurnEnergy(int turn, IEnumerable<Location> locations) =>
        MatchmakingRules.TurnEnergy(turn, locations.Select(location => new MatchLocationEnergy
        {
            LocationId = location.Id,
            EnergyMode = location.EnergyMode,
            ExtraEnergy = location.ExtraEnergy
        }), new Dictionary<long, int>());

    /// <summary>Calcula energía desde la configuración guardada y las cartas propias presentes al inicio del turno.</summary>
    private static int TurnEnergy(int turn, IReadOnlyList<MatchLocationChoice> choices, MatchPlayer player)
    {
        var locations = choices.Select(ReadLocation).ToArray();
        var countByLocation = choices.ToDictionary(
            choice => choice.LocationId,
            choice => player.PlayedLocationSlots.Count(slot => slot == choice.Slot));
        return MatchmakingRules.TurnEnergy(turn, locations.Select(location => new MatchLocationEnergy
        {
            LocationId = location.Id,
            EnergyMode = location.EnergyMode,
            ExtraEnergy = location.ExtraEnergy
        }), countByLocation);
    }

    /// <summary>Restaura la configuración inmutable de ubicación guardada con la partida.</summary>
    private static Location ReadLocation(MatchLocationChoice choice) =>
        JsonSerializer.Deserialize<Location>(choice.Snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new MatchmakingFailure("invalid_location_snapshot", "La configuración guardada de una ubicación no es válida.", 503);

    /// <summary>Convierte el id persistido de una carta a su representación pública o informa datos inconsistentes.</summary>
    private static MatchCardState ToMatchCard(IReadOnlyDictionary<long, Card> cards, long cardId)
    {
        if (!cards.TryGetValue(cardId, out var card))
            throw new MatchmakingFailure("match_card_missing", "No fue posible leer una carta de su partida.", 503);
        return new MatchCardState(card.Id, card.Name, card.Cost, card.Power, card.CardType,
            card.ImageOriginalPath, card.ImageSmallPath);
    }

    /// <summary>Comprueba si la restricción de turno configurada para una ubicación aplica al turno actual.</summary>
    private static bool IsTurnRestricted(Location location, int turn)
        => ReadRestrictedTurns(location).Contains(turn);

    /// <summary>Lee y filtra los turnos válidos de los parámetros TurnRestriction de una ubicación.</summary>
    private static IReadOnlyList<int> ReadRestrictedTurns(Location location)
    {
        if (!string.Equals(location.EffectType, "TurnRestriction", StringComparison.OrdinalIgnoreCase))
            return [];
        using var parameters = JsonDocument.Parse(location.EffectParameters);
        return parameters.RootElement.TryGetProperty("turns", out var turns)
            && turns.ValueKind == JsonValueKind.Array
            ? turns.EnumerateArray().Select(item => item.TryGetInt32(out var turn) ? turn : 0)
                .Where(turn => turn is >= 1 and <= 6).Distinct().Order().ToArray()
            : [];
    }

    /// <summary>Aplica el modificador de costo configurado para el tipo de carta y evita costos negativos.</summary>
    private static int EffectiveCost(Card card, Location location)
    {
        if (!string.Equals(location.EffectType, "CostModifier", StringComparison.OrdinalIgnoreCase))
            return card.Cost;
        using var parameters = JsonDocument.Parse(location.EffectParameters);
        if (!parameters.RootElement.TryGetProperty("amount", out var amount)
            || !amount.TryGetInt32(out var modifier))
            return card.Cost;
        if (parameters.RootElement.TryGetProperty("affectedType", out var affectedType)
            && affectedType.ValueKind == JsonValueKind.String
            && affectedType.GetString() is { Length: > 0 } type
            && !string.Equals(type, card.CardType, StringComparison.OrdinalIgnoreCase))
            return card.Cost;
        return Math.Max(0, card.Cost + modifier);
    }

    /// <summary>Convierte la búsqueda persistida y las opciones de matchmaking calculadas por el servidor al contrato API.</summary>
    private MatchSearchResult Result(MatchSearch search) => new(search.Id, search.Status, search.Context, search.ContextId,
        search.DeckId, search.Elo, MatchmakingRules.EloRange(search.CreatedAt, clock.GetUtcNow().UtcDateTime),
        search.CreatedAt, search.MatchId, search.EndReason, MatchmakingRules.CanOfferBot(search, clock.GetUtcNow().UtcDateTime));

    // Usa el mismo UUID para consultar mazos, reconocer reintentos y comprobar reservas.
    private static string NormalizeDeckId(string deckId) => Guid.TryParse(deckId, out var id) ? id.ToString() : deckId;

    /// <summary>Rechaza solicitudes sin un identificador de jugador autenticado y no vacío.</summary>
    private static void ValidateIdentity(Guid playerId)
    {
        if (playerId == Guid.Empty) throw new AuthenticationFailure("authentication_required", "Debe iniciar sesión para continuar.", 401);
    }
}
