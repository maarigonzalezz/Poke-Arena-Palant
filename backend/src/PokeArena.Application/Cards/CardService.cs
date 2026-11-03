using PokeArena.Application.Common;

namespace PokeArena.Application.Cards;

/// <summary>Coordina validación, imágenes y repositorios para los casos de uso de cartas.</summary>
public sealed class CardService(CatalogValidationService validation, ICardSearchRepository search,
    ICatalogTransaction transactions, IImageStore images)
{
    public Task<CardSearchResult> SearchAsync(string actor, CardSearchQuery query, CancellationToken cancellationToken)
    {
        // Una consulta inválida no alcanza el repositorio de búsqueda.
        validation.ValidateSearch(query);
        return search.SearchAsync(actor, query, cancellationToken);
    }

    public async Task<CardCreateResult> CreateAsync(string actor, CardCreateQuery query, ImageUpload image,
        string key, CancellationToken cancellationToken)
    {
        validation.ValidateCard(query, key);
        // Normaliza antes de calcular la huella para conservar la identidad del reintento.
        query = query with { Name = query.Name.Trim(), AbilityCode = query.AbilityCode.Trim() };
        StoredImage? stored = null;
        try
        {
            return await transactions.RunAsync(actor, key, "create-card", query, image, async session =>
            {
                // Se consulta dentro de la transacción para evitar cambios entre validar y guardar.
                var ability = validation.ValidateAbility(query,
                    await session.FindActiveAbilityAsync(query.AbilityCode, cancellationToken));
                stored = await images.SaveAsync(image, "cards", cancellationToken);
                return await session.Cards.CreateAsync(query, ability, stored, cancellationToken);
            }, cancellationToken);
        }
        catch
        {
            // Compensa los archivos creados si el guardado o la confirmación SQL fallan.
            if (stored is not null) images.Delete(stored);
            throw;
        }
    }
}
