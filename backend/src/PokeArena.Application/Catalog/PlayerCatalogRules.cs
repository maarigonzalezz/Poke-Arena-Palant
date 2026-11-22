using System.Linq.Expressions;
using PokeArena.Domain.Catalogs;
using PokeArena.Domain.Entities;

namespace PokeArena.Application.Catalog;

// Application define qué cartas puede ver el jugador; EF aplica la regla como filtro SQL.
public static class PlayerCatalogRules
{
    public static readonly Expression<Func<Card, bool>> VisibleCards =
        card => card.Active && card.ImageStatus == GameRules.ReadyImageStatus && card.ImageOriginalPath != null;
}
