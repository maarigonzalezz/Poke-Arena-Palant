using Microsoft.EntityFrameworkCore;
using Npgsql;
using PokeArena.Application.Cards;
using PokeArena.Domain.Entities;
using PokeArena.Infrastructure.Persistence;

namespace PokeArena.Infrastructure.Cards;

/// <summary>Construye filtros con LINQ y conserva los resultados en sesiones estables.</summary>
public sealed class PostgresCardSearchRepository(PokeArenaDbContext db) : ICardSearchRepository
{
    public async Task<CardSearchResult> SearchAsync(string actorId, CardSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.SearchId.HasValue)
            return await ReadPageAsync(actorId, query.SearchId.Value, query.After, query.PageSize, cancellationToken);

        var session = new CardSearchSession
        {
            Id = Guid.NewGuid(), ActorId = actorId, ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            db.CardSearchSessions.Add(session);
            await db.SaveChangesAsync(cancellationToken);
            // CreatedAt viene del servidor; el vencimiento usa el mismo reloj que las lecturas.
            session.ExpiresAt = db.Entry(session).Property<DateTime>("CreatedAt").CurrentValue.AddMinutes(30);

            // EF genera filtros y parámetros; INSERT SELECT evita cargar miles de IDs en la aplicación.
            await using var selection = Filter(query).Select(card => new { CardId = card.Id }).CreateDbCommand();
            var parameters = selection.Parameters.Cast<NpgsqlParameter>()
                .Select(parameter => (object)parameter.Clone()).ToList();
            parameters.Add(new NpgsqlParameter("snapshotSessionId", session.Id));
            var snapshotSql = $"""
                INSERT INTO card_search_items(session_id, position, card_id)
                SELECT @snapshotSessionId, ROW_NUMBER() OVER (ORDER BY matched."CardId"), matched."CardId"
                FROM ({selection.CommandText}) AS matched
                """;
            await db.Database.ExecuteSqlRawAsync(snapshotSql, parameters, cancellationToken);
            session.ResultCount = await db.CardSearchItems.Where(x => x.SessionId == session.Id)
                .LongCountAsync(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await ReadPageAsync(actorId, session.Id, 0, query.PageSize, cancellationToken);
    }

    // Cada Where conserva la combinación AND y se traduce a SQL parametrizado.
    private IQueryable<Card> Filter(CardSearchQuery query)
    {
        var cards = db.Cards.AsNoTracking().Where(card => card.Active);
        if (!string.IsNullOrWhiteSpace(query.Code))
            cards = cards.Where(card => card.Code == query.Code.Trim());
        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            // Los comodines escritos por el usuario se buscan literalmente.
            var name = query.Name.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            cards = cards.Where(card => EF.Functions.ILike(EF.Functions.Unaccent(card.Name),
                "%" + EF.Functions.Unaccent(name) + "%", "\\"));
        }
        if (query.Cost.HasValue) cards = cards.Where(card => card.Cost == query.Cost.Value);
        if (query.MinCost.HasValue) cards = cards.Where(card => card.Cost >= query.MinCost.Value);
        if (query.MaxCost.HasValue) cards = cards.Where(card => card.Cost <= query.MaxCost.Value);
        if (query.Power.HasValue) cards = cards.Where(card => card.Power == query.Power.Value);
        if (query.MinPower.HasValue) cards = cards.Where(card => card.Power >= query.MinPower.Value);
        if (query.MaxPower.HasValue) cards = cards.Where(card => card.Power <= query.MaxPower.Value);
        if (!string.IsNullOrWhiteSpace(query.Type)) cards = cards.Where(card => card.CardType == query.Type.Trim());
        if (!string.IsNullOrWhiteSpace(query.Rarity)) cards = cards.Where(card => card.Rarity == query.Rarity.Trim());
        if (!string.IsNullOrWhiteSpace(query.AbilityCode)) cards = cards.Where(card => card.AbilityCode == query.AbilityCode.Trim());
        if (!string.IsNullOrWhiteSpace(query.AbilityTrigger))
            cards = cards.Where(card => db.Abilities.Any(ability => ability.Code == card.AbilityCode
                && ability.TriggerType == query.AbilityTrigger.Trim()));
        if (!string.IsNullOrWhiteSpace(query.AbilityEffect))
            cards = cards.Where(card => db.Abilities.Any(ability => ability.Code == card.AbilityCode
                && ability.EffectType == query.AbilityEffect.Trim()));
        return cards;
    }

    private async Task<CardSearchResult> ReadPageAsync(string actorId, Guid searchId, long after,
        int pageSize, CancellationToken cancellationToken)
    {
        // Una sola consulta conserva las sesiones vacías y comprueba propiedad y vigencia.
        var rows = await (
            from session in db.CardSearchSessions.AsNoTracking()
            where session.Id == searchId && session.ActorId == actorId && session.ExpiresAt > DateTime.UtcNow
            join entry in db.CardSearchItems.Where(x => x.Position > after && x.Position <= after + pageSize)
                on session.Id equals entry.SessionId into entries
            from entry in entries.DefaultIfEmpty()
            join card in db.Cards on entry.CardId equals card.Id into cards
            from card in cards.DefaultIfEmpty()
            join ability in db.Abilities on card.AbilityCode equals ability.Code into abilities
            from ability in abilities.DefaultIfEmpty()
            orderby entry.Position
            select new
            {
                session.ResultCount, Position = (long?)entry.Position,
                card.Code, card.Name, Cost = (int?)card.Cost, Power = (int?)card.Power,
                card.CardType, card.Rarity, card.ImageStatus, card.ImageOriginalPath,
                AbilityCode = ability.Code, AbilityName = ability.Name, ability.TriggerType,
                ability.EffectType, ability.Parameters
            }).ToListAsync(cancellationToken);
        if (rows.Count == 0) throw new KeyNotFoundException("La búsqueda no existe o ha expirado.");

        var items = rows.Where(row => row.Position.HasValue).Select(row => new CardSearchItem(
            row.Code, row.Name, row.Cost!.Value, row.Power!.Value, row.CardType, row.Rarity,
            row.ImageStatus, row.ImageOriginalPath, row.AbilityCode, row.AbilityName,
            row.TriggerType, row.EffectType, row.Parameters)).ToList();
        var total = rows[0].ResultCount;
        var last = rows[^1].Position ?? after;
        return new CardSearchResult(items, searchId, last < total ? last : null, total);
    }
}
