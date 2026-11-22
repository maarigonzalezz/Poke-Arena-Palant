using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PokeArena.Application.Authentication;
using PokeArena.Infrastructure.Authentication;

namespace PokeArena.Api.Controllers;

/// <summary>Intercambia un ID token verificado de Google por una sesión propia de Poké-Arena.</summary>
[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(LoginService login, GoogleIdentityOptions google, PlatformTokenOptions platform) : ControllerBase
{
    // Solo publica configuración no secreta para preparar el botón oficial de Google.
    [AllowAnonymous]
    [HttpGet("config")]
    public IActionResult Configuration() => Ok(new
    {
        googleClientId = google.IsConfigured ? google.ClientId : null,
        enabled = google.IsConfigured && platform.IsConfigured,
        accessTokenLifetimeSeconds = platform.LifetimeMinutes * 60
    });

    [AllowAnonymous]
    [HttpPost("google")]
    [Consumes("application/json")]
    [RequestSizeLimit(16384)]
    [EnableRateLimiting("google-login")]
    public async Task<ActionResult<LoginResult>> Google([FromBody] GoogleLoginRequest request, CancellationToken cancellationToken) =>
        Ok(await login.LoginAsync(request.IdToken, cancellationToken));

    // Este endpoint acepta exclusivamente credenciales propias, incluso durante desarrollo.
    [Authorize(AuthenticationSchemes = PlatformTokenOptions.Scheme)]
    [HttpGet("me")]
    public async Task<ActionResult<PlayerProfile>> Me(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var id)) return Unauthorized();
        return Ok(await login.GetProfileAsync(id, cancellationToken));
    }
}

// El cliente no puede elegir identidad, rol ni Elo mediante el formulario de ingreso.
public sealed class GoogleLoginRequest
{
    [Required, StringLength(10000, MinimumLength = 1)]
    public string IdToken { get; init; } = "";
}
