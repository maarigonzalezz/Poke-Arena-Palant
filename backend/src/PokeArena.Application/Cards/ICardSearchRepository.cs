
namespace PokeArena.Application.Cards;

/// <summary>Contrato de persistencia para buscar cartas y continuar sesiones del mismo usuario.</summary>
public interface ICardSearchRepository
{
    Task<CardSearchResult> SearchAsync(
        string actorId,
        CardSearchQuery query,
        CancellationToken cancellationToken = default
    );
}
