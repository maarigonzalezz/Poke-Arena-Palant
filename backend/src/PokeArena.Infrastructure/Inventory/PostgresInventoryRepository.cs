using Microsoft.EntityFrameworkCore;
using Npgsql;

using PokeArena.Domain.Entities;
using PokeArena.Application.Inventory;
using PokeArena.Infrastructure.Persistence;
using PokeArena.Application.Cards;
using PokeArena.Application.Authentication;

namespace PokeArena.Infrastructure.Inventory;

public sealed class PostgresInventoryRepository(PokeArenaDbContext db) : IInventoryRepository
{   
    private static CardItem CardI(Card card, AbilityRecord ability)
    {
        return new CardItem(
            card.Id,
            card.Code,
            card.Name,
            card.Cost,
            card.Power,
            card.CardType,
            card.Rarity,
            card.ImageStatus,
            card.ImageOriginalPath,
            card.ImageSmallPath,
            ability.Code,
            ability.Name,
            ability.TriggerType,
            ability.EffectType,
            ability.Parameters
        );
    }

    private static PlayerProfile Profile(Player player)
    {
        return new PlayerProfile(player.Id, player.DisplayName, player.Email, player.Role, player.Elo, player.Cubes);
    }

    /// <summary>Consultar el inventario de un jugador. Si no tiene inventario, genera uno automaticamente.</summary>
    public async Task<PlayerProfileResult> ConsultAsync(
        PlayerProfileQuery query,
        CancellationToken cancellationToken = default
    ){
        // Buscar jugador
        var player = await db.Players
            .SingleOrDefaultAsync(p => p.Id == query.Profile.Id, cancellationToken);

        // Verificar que el jugador existe
        if (player is null)
            throw new KeyNotFoundException("No se encontró registro del jugador.");

        // Obtener coleccion de cartas del jugador
        var owned = await (
            from player_cards in db.PlayerCards.AsNoTracking()
            where player_cards.PlayerId == player.Id
            join card in db.Cards.AsNoTracking()
                on player_cards.CardId equals card.Id into cards
            from card in cards.DefaultIfEmpty()
            join ability in db.Abilities.AsNoTracking()
                on card.AbilityCode equals ability.Code into abilities
            from ability in abilities.DefaultIfEmpty()
            select CardI(card, ability)
        ).ToListAsync(cancellationToken);

        // Verificar si el jugador es nuevo
        bool new_player = query.NewPlayer || owned.Count < 15;
        if (new_player) // crear coleccion inicial para el jugador
        {   
            // Obtener el repertorio de cartas (comunes) disponibles
            var available = await (
                from card in db.Cards.AsNoTracking()
                join ability in db.Abilities.AsNoTracking()
                    on card.AbilityCode equals ability.Code
                where !db.PlayerCards.Any(playerCard =>
                    playerCard.PlayerId == player.Id &&
                    playerCard.CardId == card.Id
                ) && card.Active && card.Rarity == "Común"
                select CardI(card, ability)
            ).ToListAsync(cancellationToken);
            // Agregar el resto de cartas iniciales
            int remaining = 15 - owned.Count;

            if (available.Count < remaining)
                throw new InventoryConflictException("No hay suficientes cartas; el juego aun no está disponible.");

            while (remaining > 0)
            {
                // Seleccionar indice aleatorio
                int index = Random.Shared.Next(0,available.Count);
                // Seleccionar carta
                var chosen = available[index];
                owned.Add(chosen);
                available.Remove(chosen);
                // Descontar carta restante
                remaining -= 1;
                // Agregar relacion a la tabla colecciones
                db.PlayerCards.Add(new PlayerCard{
                    PlayerId = player.Id,
                    CardId = chosen.Id
                });
            }
            
            // Agregar los cubos iniciales
            if (player.Cubes == 0) player.Cubes = 1000;

            // Actualizar la base de datos
            await db.SaveChangesAsync(cancellationToken);
        }

        // Retornar el perfil de resultado
        return new PlayerProfileResult(
            Profile(player),
            false,
            owned
        );
    }
}