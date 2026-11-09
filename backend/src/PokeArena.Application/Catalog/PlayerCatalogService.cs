using PokeArena.Application.Cards;
using PokeArena.Application.Common;
using PokeArena.Domain.Catalogs;

namespace PokeArena.Application.Catalog;

//Valida la exploracion antes de consultar el catalogo o la coleccion.
public sealed class PlayerCatalogService(IPlayerCatalogRepository repository)
{
    public Task<PlayerCatalogPage> BrowseAsync(Guid playerId, PlayerCatalogQuery query,
        CancellationToken cancellationToken)
    {
        // Reutiliza las reglas del catalogo administrativo; su cursor de sesiones no aplica aqui.
        var errors = CardSearchValidator.Validate(new CardSearchQuery
        {
            Type = query.Type, Cost = query.Cost, Rarity = query.Rarity, PageSize = query.PageSize
        });
        if (errors.ContainsKey("type"))
            errors["type"] = $"Valores permitidos: {string.Join(", ", CardTypes.Prefixes.Keys.Order())}.";
        if (query.After < 0)
            errors["after"] = "Debe ser cero o un ID positivo recibido en nextPosition.";
        if (playerId == Guid.Empty)
            errors["playerId"] = "Se requiere un jugador valido.";
        if (errors.Count > 0)
            throw new CatalogValidationException(errors);

        return repository.BrowseAsync(playerId, query, cancellationToken);
    }
}
