using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PokeArena.Application.Cards;
using PokeArena.Infrastructure.Persistence;

namespace PokeArena.Infrastructure.Cards;

// LINQ consulta solo habilidades activas sin mantener entidades en seguimiento.
public sealed class PostgresAbilityRepository(PokeArenaDbContext db) : IAbilityRepository
{
    public async Task<IReadOnlyList<AbilityItem>> GetAsync(CancellationToken cancellationToken)
    {
        var abilities = await db.Abilities.AsNoTracking().Where(x => x.Active)
            .OrderBy(x => x.Code).ToListAsync(cancellationToken);
        return abilities.Select(ToItem).ToList();
    }

    // El JSONB se convierte al contrato público después de materializar la consulta.
    internal static AbilityItem ToItem(AbilityRecord ability) => new(ability.Code, ability.Name,
        ability.TriggerType, ability.EffectType, JsonSerializer.Deserialize<JsonElement>(ability.Parameters),
        ability.RestrictedCardType);
}
