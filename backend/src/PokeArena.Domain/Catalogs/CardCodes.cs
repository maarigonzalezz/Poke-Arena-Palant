namespace PokeArena.Domain.Catalogs;

// Decide el consecutivo y su formato; los bloqueos y la lectura del contador pertenecen al repositorio.
public static class CardCodes
{
    public const int NumericOffset = 8;
    public const int MinimumDigits = 3;

    public static int Next(long stored, long lastExisting)
    {
        var last = Math.Max(stored, lastExisting);
        if (last >= int.MaxValue) throw new InvalidOperationException("Se agotó el consecutivo disponible para este tipo.");
        return checked((int)last + 1);
    }

    public static string Format(string prefix, int number) => $"PKA-{prefix}-{number.ToString($"D{MinimumDigits}")}";
}
