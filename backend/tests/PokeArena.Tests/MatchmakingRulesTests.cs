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

    [Theory]
    [InlineData(59, 0, false)]
    [InlineData(60, 0, true)]
    [InlineData(600, 59, true)]
    [InlineData(600, 60, false)]
    public void Bot_offer_requires_enough_wait_and_a_live_search(int waited, int inactive, bool expected)
    {
        var now = DateTime.UtcNow;
        var search = Search(0, now.AddSeconds(-waited));
        search.LastPolledAt = now.AddSeconds(-inactive);
        Assert.Equal(expected, MatchmakingRules.CanOfferBot(search, now));
        search.Context = MatchmakingRules.Tournament;
        Assert.False(MatchmakingRules.CanOfferBot(search, now));
    }

    [Theory]
    [InlineData(MatchmakingRules.Cancelled)]
    [InlineData(MatchmakingRules.Expired)]
    [InlineData(MatchmakingRules.Matched)]
    public void Closed_searches_never_pair_or_offer_a_bot(string status)
    {
        var now = DateTime.UtcNow;
        var first = Search(0, now.AddMinutes(-2));
        var second = Search(0, now);
        first.Status = status;
        Assert.False(MatchmakingRules.CanPair(first, second, now));
        Assert.False(MatchmakingRules.CanPair(second, first, now));
        Assert.False(MatchmakingRules.CanOfferBot(first, now));
    }

    [Theory]
    [InlineData(59, false)]
    [InlineData(60, true)]
    [InlineData(61, true)]
    public void Inactivity_is_measured_from_the_last_player_poll(int seconds, bool expired)
    {
        var now = DateTime.UtcNow;
        var search = Search(0, now.AddMinutes(-10));
        search.LastPolledAt = now.AddSeconds(-seconds);
        search.LastCheckedAt = now;
        Assert.Equal(expired, MatchmakingRules.IsExpired(search, now));
        Assert.Equal(!expired, MatchmakingRules.CanPair(search, Search(0, now), now));
    }

    private static MatchSearch Search(int elo, DateTime created) => new() {
        Id = Guid.NewGuid(), PlayerId = Guid.NewGuid(), DeckId = "deck", CardIds = [],
        Context = MatchmakingRules.Ranked, Elo = elo, CreatedAt = created, LastPolledAt = DateTime.UtcNow
    };
}
