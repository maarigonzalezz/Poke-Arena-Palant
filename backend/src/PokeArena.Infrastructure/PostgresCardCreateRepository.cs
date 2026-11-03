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
        if (sequence is null || sequence.LastNumber >= 999)
            throw new CatalogConflictException("Se agotaron los códigos de tres dígitos para este tipo.");

        // Reconcilia el contador con cartas importadas sin cargar el catálogo en memoria.
        var pattern = $"^PKA-{prefix}-[0-9]{{3}}$";
        var lastExisting = await db.Cards.Where(x => x.CardType == query.Type && Regex.IsMatch(x.Code, pattern))
            .Select(x => (int?)Convert.ToInt32(x.Code.Substring(x.Code.Length - 3))).MaxAsync(cancellationToken) ?? 0;
        var number = Math.Max(sequence.LastNumber, lastExisting) + 1;
        if (number > 999)
            throw new CatalogConflictException("Se agotaron los códigos de tres dígitos para este tipo.");
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
