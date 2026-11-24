namespace PokeArena.Domain.Matchmaking;

// La política de Elo es compartida por las instancias y siempre se calcula en el servidor.
public static class MatchmakingRules
{
    public const int RequiredCubes = 8;
    public const int RankedWagerCubes = 4;
    public const int InitialHandSize = 3;
    public const int MaximumHandSize = 7;
    public const int TurnDurationSeconds = 30;
    public const int InitialTurn = 1;
    public const int InitialEloRange = 100;
    public const int EloRangeStep = 50;
    public const int WidenEverySeconds = 15;
    public const int MaximumEloRange = 500;
    public const int ProcessingIntervalSeconds = 2;
    public const int MaximumDeckIdLength = 100;
    public const int ProcessingBatchSize = 100;
    public const int BotOfferAfterSeconds = 60;
    public const int SearchInactivitySeconds = 60;
    public const string Ranked = "Ranked";
    public const string Tournament = "Tournament";
    public const string Searching = "Searching";
    public const string Matched = "Matched";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";
    public const string ActiveMatch = "Active";
    public const string CompletedMatch = "Completed";

    /// <summary>Calcula el rango de Elo, ampliándolo por intervalos hasta alcanzar el máximo configurado.</summary>
    public static int EloRange(DateTime startedAt, DateTime now)
    {
        var seconds = Math.Max(0, (now - startedAt).TotalSeconds);
        var steps = Math.Min((MaximumEloRange - InitialEloRange) / EloRangeStep,
            Math.Floor(seconds / WidenEverySeconds));
        return InitialEloRange + (int)steps * EloRangeStep;
    }

    // Solo las consultas del jugador renuevan su presencia; el worker no la prolonga.
    /// <summary>Indica si una búsqueda abierta lleva demasiado tiempo sin consultas del jugador.</summary>
    public static bool IsExpired(MatchSearch search, DateTime now) =>
        search.Status == Searching && (now - search.LastPolledAt).TotalSeconds >= SearchInactivitySeconds;

    /// <summary>Permite ofrecer bot solo en clasificatoria, tras la espera mínima y mientras la búsqueda siga activa.</summary>
    public static bool CanOfferBot(MatchSearch search, DateTime now) =>
        search.Status == Searching && search.Context == Ranked && !IsExpired(search, now)
        && (now - search.CreatedAt).TotalSeconds >= BotOfferAfterSeconds;

    // Ambos deben seguir presentes y aceptar la diferencia según su propio tiempo de espera.
    /// <summary>Valida presencia, contexto, identidad y compatibilidad Elo antes de emparejar dos búsquedas.</summary>
    public static bool CanPair(MatchSearch first, MatchSearch second, DateTime now) =>
        first.Status == Searching && second.Status == Searching && !IsExpired(first, now) && !IsExpired(second, now)
        && first.PlayerId != second.PlayerId && first.Context == second.Context && first.ContextId == second.ContextId
        && Math.Abs((long)first.Elo - second.Elo) <= Math.Min(EloRange(first.CreatedAt, now), EloRange(second.CreatedAt, now));

    // La energía se recalcula por turno (2 a 7) y no arrastra el sobrante anterior.
    /// <summary>Devuelve la energía base del turno: dos en el primero, incrementando hasta siete.</summary>
    public static int BaseTurnEnergy(int turn) => turn switch
    {
        <= 0 => 0,
        >= 7 => 7,
        _ => turn + 1
    };

    /// <summary>Determina el ganador por ubicaciones, energía restante y tamaño del cementerio, en ese orden.</summary>
    public static string MatchOutcome(int playerLocationsWon, int opponentLocationsWon,
        int playerEnergy, int opponentEnergy, int playerCemeteryCount, int opponentCemeteryCount)
    {
        if (playerLocationsWon != opponentLocationsWon)
            return playerLocationsWon > opponentLocationsWon ? "Player" : "Opponent";
        if (playerEnergy != opponentEnergy)
            return playerEnergy > opponentEnergy ? "Player" : "Opponent";
        if (playerCemeteryCount != opponentCemeteryCount)
            return playerCemeteryCount < opponentCemeteryCount ? "Player" : "Opponent";
        return "Draw";
    }

    // Los efectos PerCard usan solo las cartas propias que ya están en cada ubicación.
    /// <summary>Suma energía base y bonificaciones fijas o por carta propia presente en cada ubicación.</summary>
    public static int TurnEnergy(int turn, IEnumerable<MatchLocationEnergy>? locations, IReadOnlyDictionary<long, int>? ownCardsPerLocation)
    {
        var energy = BaseTurnEnergy(turn);
        foreach (var location in locations ?? [])
        {
            if (location.EnergyMode == "Fixed")
                energy += location.ExtraEnergy;
            else if (location.EnergyMode == "PerCard"
                && ownCardsPerLocation is not null
                && ownCardsPerLocation.TryGetValue(location.LocationId, out var cards))
                energy += location.ExtraEnergy * cards;
        }
        return energy;
    }

    /// <summary>Roba la primera carta si la mano tiene menos de siete y el mazo no está vacío.</summary>
    public static bool TryDrawCard(IReadOnlyList<long> hand, IReadOnlyList<long> deck,
        out long[] nextHand, out long[] nextDeck)
    {
        nextHand = hand.ToArray();
        nextDeck = deck.ToArray();
        // Al alcanzar el límite o agotarse el mazo, ambas colecciones quedan intactas.
        if (hand.Count >= MaximumHandSize || deck.Count == 0) return false;
        nextHand = hand.Append(deck[0]).ToArray();
        nextDeck = deck.Skip(1).ToArray();
        return true;
    }
}
