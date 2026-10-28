using PokeArena.Application.Common;

namespace PokeArena.Application.Cards;

// Contrato de creación: el controlador lo consume y PostgreSQL lo implementa.
public interface ICardCreateRepository
{
    Task<CardCreateResult> CreateAsync(string actorId, CardCreateQuery query, ImageUpload image,
        string idempotencyKey, CancellationToken cancellationToken = default);
}
