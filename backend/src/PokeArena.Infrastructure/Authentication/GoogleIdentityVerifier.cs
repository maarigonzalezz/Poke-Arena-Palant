using Google.Apis.Auth;
using PokeArena.Application.Authentication;

namespace PokeArena.Infrastructure.Authentication;

/// <summary>Verifica firma RS256, emisor, audiencia y vigencia con el SDK oficial de Google.</summary>
public sealed class GoogleIdentityVerifier(GoogleIdentityOptions options) : IGoogleIdentityVerifier
{
    public async Task<ExternalIdentity> VerifyAsync(string idToken, CancellationToken cancellationToken)
    {
        if (!options.IsConfigured)
            throw new AuthenticationFailure("google_not_configured", "Falta configurar el Client ID de Google en el servidor.", 503);
        try
        {
            // Nunca se omite Audience ni se aceptan certificados aportados por el cliente.
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [options.ClientId],
                    ExpirationTimeClockTolerance = TimeSpan.Zero,
                    IssuedAtClockTolerance = TimeSpan.FromSeconds(30)
                }).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            if (string.IsNullOrWhiteSpace(payload.Subject) || payload.Subject.Length > 255)
                throw new InvalidJwtException("Missing subject.");
            // El correo verificado es un dato de perfil y nunca una clave para unir cuentas.
            return new ExternalIdentity("Google", payload.Subject,
                payload.EmailVerified ? payload.Email : null, payload.Name);
        }
        catch (InvalidJwtException)
        {
            throw new AuthenticationFailure("invalid_google_token", "El token de Google es inválido, venció o pertenece a otra aplicación.", 401);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException
            || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new AuthenticationFailure("identity_provider_unavailable", "No se pudo comprobar la identidad con Google. Intente nuevamente.", 503);
        }
    }
}
