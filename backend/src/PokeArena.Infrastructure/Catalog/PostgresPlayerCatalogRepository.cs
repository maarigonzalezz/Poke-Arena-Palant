using System.Data;
using Microsoft.EntityFrameworkCore;
using PokeArena.Application.Authentication;
using PokeArena.Application.Catalog;
using PokeArena.Infrastructure.Persistence;

namespace PokeArena.Infrastructure.Catalog;

/// <summary>Combina catalogo y pertenencia en SQL sin cargar el inventario completo.</summary>
public sealed class PostgresPlayerCatalogRepository(PokeArenaDbContext db) : IPlayerCatalogRepository
{
    public async Task<PlayerCatalogPage> BrowseAsync(Guid playerId, PlayerCatalogQuery query,
        CancellationToken cancellationToken)
    {
        // Conteo y pagina observan los mismos datos si la coleccion cambia durante la solicitud.
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var active = await db.Players.Where(player => player.Id == playerId)
            .Select(player => (bool?)player.Active).SingleOrDefaultAsync(cancellationToken);
        if (active is null)
            throw new AuthenticationFailure("account_not_found", "La cuenta ya no esta disponible.", 401);
        if (!active.Value)
            throw new AuthenticationFailure("account_disabled", "La cuenta esta deshabilitada.", 403);

        // El LEFT JOIN conserva cartas no poseidas y restringe la pertenencia al jugador del token.
        var cards =
            from card in db.Cards.AsNoTracking()
            where card.Active && card.ImageStatus == "Ready" && card.ImageOriginalPath != null
            join ability in db.Abilities.AsNoTracking() on card.AbilityCode equals ability.Code
            join owned in db.PlayerCards.Where(entry => entry.PlayerId == playerId)
                on card.Id equals owned.CardId into collection
            from owned in collection.DefaultIfEmpty()
            select new { Card = card, Ability = ability, Quantity = owned == null ? 0 : owned.Copies };

        if (query.Type is not null) cards = cards.Where(row => row.Card.CardType == query.Type);
        if (query.Cost.HasValue) cards = cards.Where(row => row.Card.Cost == query.Cost.Value);
        if (query.Rarity is not null) cards = cards.Where(row => row.Card.Rarity == query.Rarity);
        if (query.OwnedOnly) cards = cards.Where(row => row.Quantity > 0);

        // El total incluye todos los resultados filtrados, no solo los posteriores al cursor.
        var total = await cards.LongCountAsync(cancellationToken);
        var items = await cards.Where(row => row.Card.Id > query.After).OrderBy(row => row.Card.Id)
            .Take(query.PageSize + 1)
            .Select(row => new PlayerCatalogItem(
                row.Card.Id, row.Card.Code, row.Card.Name, row.Card.Cost, row.Card.Power,
                row.Card.CardType, row.Card.Rarity, row.Card.ImageStatus, row.Card.ImageOriginalPath,
                row.Ability.Code, row.Ability.Name, row.Ability.TriggerType, row.Ability.EffectType,
                row.Ability.Parameters, row.Quantity))
            .ToListAsync(cancellationToken);

        // Una fila extra permite saber si hay otra pagina sin devolverla al cliente.
        long? next = null;
        if (items.Count > query.PageSize)
        {
            items.RemoveAt(items.Count - 1);
            next = items[^1].Id;
        }
        await snapshot.CommitAsync(cancellationToken);
        return new PlayerCatalogPage(items, next, total);
    }
}
