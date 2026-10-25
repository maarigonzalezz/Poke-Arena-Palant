
namespace PokeArena.Application.Cards;

/// <summary>Contrato de persistencia para crear cartas.</summary>
public interface ICardCreateRepository
{
    Task<CardCreateResult> CreateAsync(
        string actorId,
        CardCreateQuery query,
        CardFileImage image,
        Stream imageContent,
        CancellationToken cancellationToken = default
    );
}