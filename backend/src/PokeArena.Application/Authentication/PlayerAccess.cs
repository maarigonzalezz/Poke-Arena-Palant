using PokeArena.Domain.Entities;

namespace PokeArena.Application.Authentication;

// Una sola regla para autorizar cuentas consultadas por los casos de uso.
public static class PlayerAccess
{
    public static Player RequireActive(Player? player)
    {
        if (player is null) throw new AuthenticationFailure("account_not_found", "La cuenta ya no está disponible.", 401);
        if (!player.Active) throw new AuthenticationFailure("account_disabled", "La cuenta está deshabilitada.", 403);
        return player;
    }
}
