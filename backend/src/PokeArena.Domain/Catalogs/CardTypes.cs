using System.Collections.Frozen;

namespace PokeArena.Domain.Catalogs;

/// <summary>Fuente única de tipos y abreviaturas para las reglas del backend.</summary>
public static class CardTypes
{
    // La colección inmutable evita que una operación cambie el catálogo compartido.
    public static IReadOnlyDictionary<string, string> Prefixes { get; } =
        new Dictionary<string, string>
        {
            ["Fuego"] = "FUE", ["Agua"] = "AGU", ["Planta"] = "PLA",
            ["Eléctrico"] = "ELE", ["Psíquico"] = "PSI", ["Lucha"] = "LUC"
        }.ToFrozenDictionary(StringComparer.Ordinal);

    // Creación, búsqueda y efectos de ubicación consultan los mismos valores.
    public static bool Contains(string? type) => type is not null && Prefixes.ContainsKey(type);

    // El servicio valida el tipo antes de pedir su abreviatura al persistir la carta.
    public static string GetPrefix(string type) => Prefixes[type];
}
