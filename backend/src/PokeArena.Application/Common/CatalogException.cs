namespace PokeArena.Application.Common;

/// <summary>Errores de negocio que la API puede devolver sin exponer detalles internos.</summary>
public sealed class CatalogValidationException(Dictionary<string, string> errors, string? type = null,
    string message = "Los datos del catálogo son inválidos.") : Exception(message)
{
    public Dictionary<string, string> Errors { get; } = errors;
    // Conserva el código público del error de búsqueda al mover la validación a Application.
    public string? Type { get; } = type;
}

public sealed class CatalogConflictException(string message) : Exception(message);
