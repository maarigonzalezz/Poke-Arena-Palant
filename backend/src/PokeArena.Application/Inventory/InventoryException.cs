
namespace PokeArena.Application.Inventory;

/// <summary>Errores de negocio que la API puede devolver sin exponer detalles internos.</summary>
public sealed class InventoryValidationException(Dictionary<string, string> errors, string? type = null,
    string message = "Los datos del inventario son inválidos.") : Exception(message)
{
    public Dictionary<string, string> Errors { get; } = errors;
    // Conserva el código público del error de búsqueda al mover la validación a Application.
    public string? Type { get; } = type;
}

public sealed class InventoryConflictException(string message) : Exception(message);