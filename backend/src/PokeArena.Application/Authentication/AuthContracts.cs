using PokeArena.Domain.Entities;

namespace PokeArena.Application.Authentication;

// Este resultado solo puede proceder del adaptador que verifica la firma del proveedor.
public sealed record ExternalIdentity(string Provider, string Subject, string? Email, string? DisplayName);
public sealed record PlayerRegistration(Player Player, bool IsNewPlayer);
public sealed record PlayerProfile(Guid Id, string? DisplayName, string? Email, string Role, int Elo, int Cubes);
public sealed record AccessCredential(string AccessToken, string TokenType, int ExpiresIn, DateTime ExpiresAt);
public sealed record LoginResult(string AccessToken, string TokenType, int ExpiresIn, DateTime ExpiresAt,
    PlayerProfile Player, bool IsNewPlayer);

public interface IGoogleIdentityVerifier
{
    Task<ExternalIdentity> VerifyAsync(string idToken, CancellationToken cancellationToken);
}

public interface IPlayerRepository
{
    Task<PlayerRegistration> GetOrCreateAsync(ExternalIdentity identity, CancellationToken cancellationToken);
    Task<Player?> FindAsync(Guid id, CancellationToken cancellationToken);
}

public interface IPlatformTokenIssuer
{
    void EnsureConfigured();
    AccessCredential Issue(Player player);
}

// Los mensajes públicos no contienen tokens, claves ni detalles de validación criptográfica.
public sealed class AuthenticationFailure(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
