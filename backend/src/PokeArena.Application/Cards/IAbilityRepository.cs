using System.Text.Json;

namespace PokeArena.Application.Cards;

// Transporta la habilidad y su restricción de tipo desde PostgreSQL hasta el frontend.
public sealed record AbilityItem(string Code, string Name, string? Trigger, string? Effect,
    JsonElement Parameters, string? RestrictedCardType);

// El controlador depende de este contrato, no del repositorio concreto.
public interface IAbilityRepository
{
    Task<IReadOnlyList<AbilityItem>> GetAsync(CancellationToken cancellationToken);
}
