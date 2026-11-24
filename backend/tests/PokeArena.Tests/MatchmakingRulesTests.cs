using PokeArena.Domain.Matchmaking;
using PokeArena.Domain.Entities;

namespace PokeArena.Tests;

public sealed class MatchmakingRulesTests
{
    [Fact]
    public void Match_preparation_is_deterministic_and_deals_three_cards()
    {
        var seed = 123456789L;
        var deck = Enumerable.Range(1, 12).Select(x => (long)x).ToArray();
        var first = MatchPreparationRules.ShuffleDeck(deck, seed);
        var second = MatchPreparationRules.ShuffleDeck(deck, seed);

        Assert.Equal(first, second);
        Assert.Equal(MatchmakingRules.InitialHandSize,
            MatchPreparationRules.OpeningHand(first).Count);
        Assert.Empty(deck.Except(first));
    }

    [Fact]
    public void Location_selection_requires_two_playable_unrestricted_locations()
    {
        var catalog = new[]
        {
            Location("a", 1, "None"),
            Location("b", 4, "None"),
            Location("c", 4, "TurnRestriction"),
            Location("d", 0, "None")
        };

        var chosen = MatchPreparationRules.ChooseLocations(catalog, 42);

        Assert.Equal(3, chosen.Count);
        Assert.Equal(2, chosen.Count(MatchPreparationRules.IsPlayableAndUnrestricted));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(5, 6)]
    [InlineData(6, 7)]
    public void Base_turn_energy_matches_the_sequence(int turn, int expected)
        => Assert.Equal(expected, MatchmakingRules.BaseTurnEnergy(turn));

    [Fact]
    public void Extra_fixed_and_per_card_energy_are_added_at_turn_start()
    {
        var locations = new[]
        {
            new MatchLocationEnergy { LocationId = 1, EnergyMode = "Fixed", ExtraEnergy = 2 },
            new MatchLocationEnergy { LocationId = 2, EnergyMode = "PerCard", ExtraEnergy = 1 }
        };
        var ownCards = new Dictionary<long, int> { [1] = 0, [2] = 3 };

        Assert.Equal(9, MatchmakingRules.TurnEnergy(3, locations, ownCards));
    }

    [Fact]
    public void Drawing_respects_the_seven_card_hand_limit()
    {
        Assert.True(MatchmakingRules.TryDrawCard([1, 2], [3, 4], out var hand, out var deck));
        Assert.Equal([1, 2, 3], hand);
        Assert.Equal([4], deck);

        Assert.False(MatchmakingRules.TryDrawCard(Enumerable.Range(1, 7).Select(x => (long)x).ToArray(),
            [8], out hand, out deck));
        Assert.Equal(7, hand.Length);
        Assert.Equal([8], deck);
    }

    [Theory]
    [InlineData(2, 1, 0, 7, 0, 4, "Player")]
    [InlineData(1, 1, 7, 6, 0, 0, "Player")]
    [InlineData(1, 1, 6, 6, 2, 3, "Player")]
    [InlineData(1, 1, 6, 6, 3, 3, "Draw")]
    [InlineData(1, 2, 7, 0, 0, 0, "Opponent")]
    public void Match_outcome_uses_location_wins_then_energy_then_cemetery(
        int playerLocations, int opponentLocations, int playerEnergy, int opponentEnergy,
        int playerCemetery, int opponentCemetery, string expected)
        => Assert.Equal(expected, MatchmakingRules.MatchOutcome(playerLocations, opponentLocations,
            playerEnergy, opponentEnergy, playerCemetery, opponentCemetery));

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

    private static Location Location(string name, int maxCards, string effectType) => new()
    {
        Id = (name[0] - 'a' + 1),
        Name = name,
        Description = name,
        MaxCards = maxCards,
        EnergyMode = "Fixed",
        ExtraEnergy = 0,
        EffectType = effectType,
        EffectParameters = "{}",
        ImageStatus = "Ready",
        CurrentVersion = 1,
        Active = true
    };
}
