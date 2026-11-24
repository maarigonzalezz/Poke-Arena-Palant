using System.Security.Cryptography;
using PokeArena.Domain.Entities;

namespace PokeArena.Domain.Matchmaking;

public static class MatchPreparationRules
{
    public static long CreateSeed() =>
        unchecked((long)RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue) ^ (DateTime.UtcNow.Ticks & 0x7fffffffffffffff));

    public static IReadOnlyList<Location> ChooseLocations(IReadOnlyList<Location> catalog, long seed)
    {
        var available = catalog.Where(x => x.Active && x.ImageStatus == "Ready").ToList();
        var unrestricted = available.Where(IsPlayableAndUnrestricted).ToList();
        if (unrestricted.Count < 2)
            throw new InvalidOperationException("No hay al menos dos ubicaciones jugables sin restricción de turno.");
        if (available.Count < 3)
            throw new InvalidOperationException("No hay tres ubicaciones disponibles.");

        var rng = new SeededRandom(seed);
        var playable = Shuffle(unrestricted, rng).Take(2).ToList();
        var remaining = Shuffle(available.Where(location => playable.All(x => x.Id != location.Id)).ToList(), rng);
        playable.Add(remaining[0]);
        return Shuffle(playable, rng);
    }

    public static IReadOnlyList<long> ShuffleDeck(IReadOnlyList<long> cardIds, long seed)
    {
        var rng = new SeededRandom(seed);
        return Shuffle(cardIds, rng).ToArray();
    }

    public static IReadOnlyList<long> OpeningHand(IReadOnlyList<long> shuffledDeck)
        => shuffledDeck.Take(MatchmakingRules.InitialHandSize).ToArray();

    public static bool IsPlayableAndUnrestricted(Location location) =>
        location.MaxCards > 0
        && !string.Equals(location.EffectType, "TurnRestriction", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<T> Shuffle<T>(IReadOnlyList<T> items, SeededRandom rng)
    {
        var result = items.ToArray();
        for (var i = result.Length - 1; i > 0; i--)
        {
            var j = rng.NextInt(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }
        return result;
    }

    private sealed class SeededRandom
    {
        private ulong state;

        public SeededRandom(long seed) => state = unchecked((ulong)seed);

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) return 0;
            state += 0x9E3779B97F4A7C15UL;
            var z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (int)(z % (uint)maxExclusive);
        }
    }
}
