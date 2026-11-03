using PokeArena.Application.Common;

namespace PokeArena.Application.Cards;

// Persiste una carta ya validada dentro de la transacción del servicio.
public interface ICardCreateRepository
{
    Task<CardCreateResult> CreateAsync(CardCreateQuery query, AbilityItem ability, StoredImage image,
        CancellationToken cancellationToken);
}
