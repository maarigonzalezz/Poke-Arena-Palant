namespace PokeArena.Application.Catalog;

//Filtros combinados con AND; After es el ID recibido en NextPosition.
public sealed record PlayerCatalogQuery
{
    public string? Type { get; init; }
    public int? Cost { get; init; }
    public string? Rarity { get; init; }
    public bool OwnedOnly { get; init; }
    public long After { get; init; }
    public int PageSize { get; init; } = 12;
}

//Quantity refleja player_cards.copies para el jugador que realiza la consulta.
public sealed record PlayerCatalogItem(
    long Id, string Code, string Name, int Cost, int Power, string Type, string Rarity,
    string ImageStatus, string? ImageUrl, string AbilityCode, string AbilityName,
    string? AbilityTrigger, string? AbilityEffect, string AbilityParameters, int Quantity)
{
    public bool Owned => Quantity > 0;
}

public sealed record PlayerCatalogPage(
    IReadOnlyList<PlayerCatalogItem> Items, long? NextPosition, long TotalCount);

public interface IPlayerCatalogRepository
{
    Task<PlayerCatalogPage> BrowseAsync(Guid playerId, PlayerCatalogQuery query, CancellationToken cancellationToken);
}
