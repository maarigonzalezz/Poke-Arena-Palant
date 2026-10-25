using PokeArena.Application.Common;

namespace PokeArena.Application.Cards;

public interface ICardCreateRepository
{
    Task<CardCreateResult> CreateAsync(string actorId, CardCreateQuery query, ImageUpload image,
        string idempotencyKey, CancellationToken cancellationToken = default);
}
