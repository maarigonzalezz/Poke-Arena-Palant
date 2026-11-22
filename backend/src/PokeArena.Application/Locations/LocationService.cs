using PokeArena.Application.Common;
using PokeArena.Domain.Entities;

namespace PokeArena.Application.Locations;

/// <summary>Coordina las reglas y el guardado de una ubicación desde Application.</summary>
public sealed class LocationService(CatalogValidationService validation,
    ICatalogTransaction transactions, IImageStore images)
{
    public async Task<Location> CreateAsync(string actor, CreateLocationRequest request, ImageUpload image,
        string key, CancellationToken cancellationToken)
    {
        // Los límites y efectos se revisan antes de abrir conexiones o generar imágenes.
        validation.ValidateLocation(request, key);
        StoredImage? stored = null;
        try
        {
            return await transactions.RunAsync(actor, key, "create-location", request, image, async session =>
            {
                // Un reintento confirmado omite este bloque y devuelve la respuesta guardada.
                stored = await images.SaveAsync(image, "locations", cancellationToken);
                return await session.Locations.CreateAsync(request, stored, cancellationToken);
            }, cancellationToken);
        }
        catch
        {
            // La transacción revierte SQL; este paso retira las imágenes de la operación fallida.
            if (stored is not null) images.Delete(stored);
            throw;
        }
    }
}
