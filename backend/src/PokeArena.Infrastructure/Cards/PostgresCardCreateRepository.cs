using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PokeArena.Application.Cards;
using PokeArena.Application.Common;
using PokeArena.Domain.Catalogs;
using PokeArena.Domain.Entities;
using PokeArena.Infrastructure.Persistence;

namespace PokeArena.Infrastructure.Cards;

/// <summary>Persiste carta y versión con EF, conservando el contador transaccional por tipo.</summary>
internal sealed class PostgresCardCreateRepository(PokeArenaDbContext db) : ICardCreateRepository
{
    public async Task<CardCreateResult> CreateAsync(CardCreateQuery query, AbilityItem ability, StoredImage image,
        CancellationToken cancellationToken)
    {
        var prefix = CardTypes.GetPrefix(query.Type);
        // LINQ no expresa FOR UPDATE; el bloqueo evita asignar el mismo consecutivo en paralelo.
        var sequence = await db.CardCodeSequences.FromSql($"""
            SELECT * FROM card_code_sequences WHERE card_type={query.Type} FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken);
        if (sequence is null)
            throw new CatalogConflictException("No existe un contador para este tipo de carta.");

        // Reconcilia el contador con cartas importadas sin cargar el catálogo en memoria.
        var pattern = $"^PKA-{prefix}-[0-9]{{3,}}$";
        var lastExisting = await db.Cards.Where(x => x.CardType == query.Type && Regex.IsMatch(x.Code, pattern))
            .Select(x => (long?)Convert.ToInt64(x.Code.Substring(8))).MaxAsync(cancellationToken) ?? 0;
        var lastNumber = Math.Max(sequence.LastNumber, lastExisting);
        if (lastNumber >= int.MaxValue)
            throw new CatalogConflictException("Se agotó el consecutivo disponible para este tipo.");
        // D3 conserva 001 y permite continuar con 1000 sin cambiar códigos existentes.
        var number = (int)lastNumber + 1;
        sequence.LastNumber = number;

        var card = new Card
        {
            Code = $"PKA-{prefix}-{number:D3}", Name = query.Name, Cost = query.Cost, Power = query.Power,
            CardType = query.Type, Rarity = query.Rarity, AbilityCode = query.AbilityCode,
            ImageStatus = "Ready", ImageOriginalPath = image.OriginalUrl, ImageSmallPath = image.SmallUrl,
            CurrentVersion = 1, Active = true
        };
        db.Cards.Add(card);
        // SaveChanges obtiene el ID, pero la transacción externa aún puede revertir todo.
        await db.SaveChangesAsync(cancellationToken);
        var item = new CardCreateItem(card.Id, card.Code, card.Name, card.Cost, card.Power,
            card.CardType, card.Rarity, card.ImageStatus, card.ImageOriginalPath, card.AbilityCode,
            ability.Name, ability.Trigger, ability.Effect, ability.Parameters.GetRawText(), image.SmallUrl);
        db.CardVersions.Add(new CardVersion
        {
            CardId = card.Id, Version = 1,
            Snapshot = JsonSerializer.Serialize(item, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        });
        await db.SaveChangesAsync(cancellationToken);
        return new CardCreateResult(item);
    }
}
