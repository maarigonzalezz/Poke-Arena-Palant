namespace PokeArena.Application.Inventory;

public sealed class InventoryValidationService
{
    // Valida la identidad del servidor; el cliente ya no envía perfil ni saldo.
    public void ValidatePlayer(Guid playerId)
    {
        if (playerId == Guid.Empty)
            throw new InventoryValidationException(new() { ["playerId"] = "Jugador inválido." });
    }

    // La regla disponible de deck inicial exige doce cartas distintas de la colección.
    public static bool CanBuildStarterDeck(IEnumerable<long> cards) => cards.Distinct().Take(12).Count() == 12;
}
