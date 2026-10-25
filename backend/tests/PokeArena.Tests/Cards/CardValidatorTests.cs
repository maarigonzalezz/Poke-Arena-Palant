using PokeArena.Application.Cards;

namespace PokeArena.Tests.Cards;

public sealed class CardValidatorTests
{
    private static CardCreateQuery Valid => new() { Name="Carta",Cost=0,Power=-20,Type="Fuego",Rarity="Común",AbilityCode="HAB-000" };

    [Fact] public void Valid_creation_accepts_boundary_values() => Assert.Empty(CardCreateValidator.Validate(Valid));

    [Theory]
    [InlineData("", "name")]
    [InlineData("   ", "name")]
    public void Blank_name_is_rejected(string name, string field) => Assert.Contains(field, CardCreateValidator.Validate(Valid with { Name=name }).Keys);

    [Fact] public void Rejects_all_invalid_creation_fields()
    {
        var errors = CardCreateValidator.Validate(Valid with { Name=new string('x',121),Cost=11,Power=21,Type="Otro",Rarity="Otra",AbilityCode="" });
        Assert.Equal(6, errors.Count);
    }

    [Theory]
    [InlineData(-1, 0)] [InlineData(11, 0)] [InlineData(1, -21)] [InlineData(1, 21)]
    public void Search_rejects_out_of_bounds(int cost, int power) => Assert.NotEmpty(CardSearchValidator.Validate(new() { Cost=cost, Power=power }));

    [Fact] public void Search_rejects_mixed_filters_and_sessions()
    {
        Assert.Contains("cost", CardSearchValidator.Validate(new() { Cost=0, MinCost=0 }).Keys);
        Assert.Contains("powerRange", CardSearchValidator.Validate(new() { MinPower=5, MaxPower=2 }).Keys);
        Assert.Contains("searchId", CardSearchValidator.Validate(new() { SearchId=Guid.NewGuid(), Name="a" }).Keys);
        Assert.Contains("after", CardSearchValidator.Validate(new() { SearchId=Guid.NewGuid(), After=long.MaxValue }).Keys);
        Assert.Contains("type", CardSearchValidator.Validate(new() { Type="Otro" }).Keys);
        Assert.Contains("rarity", CardSearchValidator.Validate(new() { Rarity="Otra" }).Keys);
    }
}
