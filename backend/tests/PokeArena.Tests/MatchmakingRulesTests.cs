using PokeArena.Domain.Matchmaking;

namespace PokeArena.Tests;

public sealed class MatchmakingRulesTests
{
    [Theory]
    [InlineData(-5,100)]
    [InlineData(0,100)]
    [InlineData(14,100)]
    [InlineData(15,150)]
    [InlineData(119,450)]
    [InlineData(120,500)]
    [InlineData(3600,500)]
    public void Elo_range_widens_at_the_documented_boundaries(int seconds, int expected)
    {
        var now = DateTime.UtcNow;
        Assert.Equal(expected, MatchmakingRules.EloRange(now.AddSeconds(-seconds), now));
    }

    [Fact]
    public void Both_players_must_accept_the_difference_and_self_pairing_is_impossible()
    {
        var now = DateTime.UtcNow;
        var first = Search(0, now.AddMinutes(-5));
        var second = Search(500, now);
        Assert.False(MatchmakingRules.CanPair(first, second, now));
        second.CreatedAt = now.AddMinutes(-2);
        Assert.True(MatchmakingRules.CanPair(first, second, now));
        second.Elo = 501;
        Assert.False(MatchmakingRules.CanPair(first, second, now));
        Assert.False(MatchmakingRules.CanPair(first, first, now));
    }

    private static MatchSearch Search(int elo, DateTime created) => new() {
        Id = Guid.NewGuid(), PlayerId = Guid.NewGuid(), DeckId = "deck", CardIds = [],
        Context = MatchmakingRules.Ranked, Elo = elo, CreatedAt = created
    };
}
