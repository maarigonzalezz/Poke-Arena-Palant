
namespace PokeArena.Application.Cards;

/// <summary>Filtros combinados con AND o datos para continuar una búsqueda existente.</summary>
public sealed record CardSearchQuery
{
    public string? Code { get; init; }
    public string? Name { get; init; }

    public int? Cost { get; init; }
    public int? MinCost { get; init; }
    public int? MaxCost { get; init; }

    public int? Power { get; init; }
    public int? MinPower { get; init; }
    public int? MaxPower { get; init; }

    public string? Type { get; init; }
    public string? Rarity { get; init; }

    public string? AbilityCode { get; init; }
    public string? AbilityTrigger { get; init; }
    public string? AbilityEffect { get; init; }

    // Al continuar una sesión no se admiten filtros nuevos.
    public Guid? SearchId { get; init; }

    // Última posición recibida; cero inicia la primera página.
    public long After { get; init; }

    public int PageSize { get; init; } = 20;
}
