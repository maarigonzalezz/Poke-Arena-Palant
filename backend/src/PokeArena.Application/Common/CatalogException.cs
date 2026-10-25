namespace PokeArena.Application.Common;

/// <summary>Errores de negocio que la API puede devolver sin exponer detalles internos.</summary>
public sealed class CatalogValidationException(Dictionary<string, string> errors)
    : Exception("Los datos del catálogo son inválidos.")
{
    public Dictionary<string, string> Errors { get; } = errors;
}

public sealed class CatalogConflictException(string message) : Exception(message);
