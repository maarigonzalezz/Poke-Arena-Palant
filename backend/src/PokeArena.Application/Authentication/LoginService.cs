using PokeArena.Application.Inventory;

namespace PokeArena.Application.Authentication;

/// <summary>Valida Google, recupera la cuenta local y entrega una credencial propia de corta duración.</summary>
public sealed class LoginService(IGoogleIdentityVerifier google, IPlayerRepository players, IPlatformTokenIssuer tokens,
    InventoryService inventory)
{
    public async Task<LoginResult> LoginAsync(string idToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idToken) || idToken.Length > 10000)
            throw new AuthenticationFailure("invalid_request", "Se requiere un ID token de Google válido.", 400);
        // Evita crear cuentas si el servidor todavía no puede emitir credenciales propias.
        tokens.EnsureConfigured();
        var identity = await google.VerifyAsync(idToken, cancellationToken);
        var registration = await players.GetOrCreateAsync(identity, cancellationToken);
        EnsureActive(registration.Player);
        // La cuenta ya existe; una entrega pendiente se intenta también en ingresos posteriores.
        var initialCollection = await inventory.EnsureInitialCollectionAsync(registration.Player.Id, cancellationToken);
        var profile = await GetProfileAsync(registration.Player.Id, cancellationToken);
        var credential = tokens.Issue(registration.Player);
        return new LoginResult(credential.AccessToken, credential.TokenType, credential.ExpiresIn,
            credential.ExpiresAt, profile, registration.IsNewPlayer, initialCollection);
    }

    public async Task<PlayerProfile> GetProfileAsync(Guid id, CancellationToken cancellationToken)
    {
        var player = await players.FindAsync(id, cancellationToken)
            ?? throw new AuthenticationFailure("account_not_found", "La cuenta ya no está disponible.", 401);
        EnsureActive(player);
        return Profile(player);
    }

    private static void EnsureActive(Domain.Entities.Player player)
    {
        if (!player.Active)
            throw new AuthenticationFailure("account_disabled", "La cuenta está deshabilitada.", 403);
    }

    private static PlayerProfile Profile(Domain.Entities.Player player) =>
        new(player.Id, player.DisplayName, player.Email, player.Role, player.Elo, player.Cubes);
}
