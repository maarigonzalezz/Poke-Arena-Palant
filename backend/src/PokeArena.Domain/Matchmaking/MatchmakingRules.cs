namespace PokeArena.Domain.Matchmaking;

// La política de Elo es compartida por las instancias y siempre se calcula en el servidor.
public static class MatchmakingRules
{
    public const int RequiredCubes = 8;
    public const int InitialEloRange = 100;
    public const int EloRangeStep = 50;
    public const int WidenEverySeconds = 15;
    public const int MaximumEloRange = 500;
    public const int ProcessingIntervalSeconds = 2;
    public const int MaximumDeckIdLength = 100;
    public const int ProcessingBatchSize = 100;
    public const string Ranked = "Ranked";
    public const string Tournament = "Tournament";
    public const string Searching = "Searching";
    public const string Matched = "Matched";
    public const string Cancelled = "Cancelled";
    public const string ActiveMatch = "Active";

    public static int EloRange(DateTime startedAt, DateTime now)
    {
        var seconds = Math.Max(0, (now - startedAt).TotalSeconds);
        var steps = Math.Min((MaximumEloRange - InitialEloRange) / EloRangeStep,
            Math.Floor(seconds / WidenEverySeconds));
        return InitialEloRange + (int)steps * EloRangeStep;
    }

    // Ambos jugadores deben aceptar la diferencia según su propio tiempo de espera.
    public static bool CanPair(MatchSearch first, MatchSearch second, DateTime now) =>
        first.PlayerId != second.PlayerId && first.Context == second.Context && first.ContextId == second.ContextId
        && Math.Abs((long)first.Elo - second.Elo) <= Math.Min(EloRange(first.CreatedAt, now), EloRange(second.CreatedAt, now));
}
