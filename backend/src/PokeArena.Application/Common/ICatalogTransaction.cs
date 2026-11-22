using PokeArena.Application.Cards;
using PokeArena.Application.Locations;

namespace PokeArena.Application.Common;

/// <summary>Agrupa el guardado y los reintentos sin exponer Npgsql a Application.</summary>
public interface ICatalogTransaction
{
    Task<T> RunAsync<T>(string actor, string key, string operation, object data, ImageUpload image,
        Func<ICatalogWriteSession, Task<T>> create, CancellationToken cancellationToken);
}

/// <summary>Acceso a los repositorios que comparten una misma transacción.</summary>
public interface ICatalogWriteSession
{
    ICardCreateRepository Cards { get; }
    ILocationRepository Locations { get; }

    // Mantiene estable la habilidad mientras el servicio valida y guarda la carta.
    Task<AbilityItem?> FindActiveAbilityAsync(string code, CancellationToken cancellationToken);
}
