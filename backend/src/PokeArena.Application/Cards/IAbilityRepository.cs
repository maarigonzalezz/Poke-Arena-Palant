using System.Text.Json;

namespace PokeArena.Application.Cards;

public sealed record AbilityItem(string Code, string Name, string? Trigger, string? Effect,
    JsonElement Parameters, string? RestrictedCardType);

public interface IAbilityRepository
{
    Task<IReadOnlyList<AbilityItem>> GetAsync(CancellationToken cancellationToken);
}
