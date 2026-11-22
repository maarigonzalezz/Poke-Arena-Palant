using PokeArena.Application.Authentication;

namespace PokeArena.Infrastructure.Authentication;

// El Client ID es público; se configura en el servidor para fijar la audiencia aceptada.
public sealed class GoogleIdentityOptions
{
    public string ClientId { get; set; } = "";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId)
        && ClientId.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal);
}

// La clave de firma solo reside en User Secrets o variables del servidor.
public sealed class PlatformTokenOptions
{
    public const string Scheme = "Platform";
    public string Issuer { get; set; } = "PokeArena";
    public string Audience { get; set; } = "PokeArena.Api";
    public string SigningKeyBase64 { get; set; } = "";
    public int LifetimeMinutes { get; set; } = 15;

    public byte[]? GetSigningKey()
    {
        try
        {
            var bytes = Convert.FromBase64String(SigningKeyBase64);
            return bytes.Length >= 32 ? bytes : null;
        }
        catch (FormatException) { return null; }
    }

    public bool IsConfigured => GetSigningKey() is not null && LifetimeMinutes is >= 1 and <= 30
        && !string.IsNullOrWhiteSpace(Issuer) && !string.IsNullOrWhiteSpace(Audience);

    public void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new AuthenticationFailure("platform_auth_not_configured", "Falta configurar la firma de credenciales de Poké-Arena.", 503);
    }
}
