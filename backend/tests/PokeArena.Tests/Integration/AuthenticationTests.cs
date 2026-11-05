using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using PokeArena.Application.Authentication;
using PokeArena.Infrastructure.Authentication;

namespace PokeArena.Tests.Integration;

// A diferencia del fixture administrativo, conserva íntegra la autenticación real de Program.
public sealed class AuthenticationFixture() : CatalogFixture(true);
[CollectionDefinition("authentication-integration")]
public sealed class AuthenticationCollection : ICollectionFixture<AuthenticationFixture>;

// El recorrido positivo por Google requiere credenciales emitidas realmente por el proveedor.
public sealed class LiveGoogleFactAttribute : FactAttribute
{
    public LiveGoogleFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("POKEARENA_INTEGRATION_TESTS") != "1"
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POKEARENA_GOOGLE_CLIENT_ID"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POKEARENA_GOOGLE_ID_TOKEN")))
            Skip = "Requiere Docker, Client ID y un ID token real y vigente de Google.";
    }
}

[Collection("authentication-integration")]
[Trait("Category", "Integration")]
public sealed class AuthenticationTests(AuthenticationFixture fixture)
{
    // Datos de persistencia: esta ayuda no sustituye la validación del endpoint de Google.
    private async Task<PlayerRegistration> Register(string subject, string? email = "profile@example.test")
    {
        await using var scope = fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetOrCreateAsync(
            new ExternalIdentity("Google", subject, email, "Jugador de integración"), CancellationToken.None);
    }

    [IntegrationFact]
    public async Task First_registration_is_atomic_and_preserves_role_and_elo_on_subsequent_logins()
    {
        await fixture.Reset();
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Register("concurrent-sub")));
        Assert.Single(results.Select(x => x.Player.Id).Distinct());
        Assert.Single(results, x => x.IsNewPlayer);
        Assert.All(results, x => { Assert.Equal("Player", x.Player.Role); Assert.Equal(0, x.Player.Elo); });
        var id = results[0].Player.Id;
        await fixture.Execute($"UPDATE players SET elo=420, role='ContentAdmin' WHERE id='{id}'");
        var again = await Register("concurrent-sub", "updated@example.test");
        Assert.False(again.IsNewPlayer); Assert.Equal(id, again.Player.Id);
        Assert.Equal(420, again.Player.Elo); Assert.Equal("ContentAdmin", again.Player.Role);
        Assert.Equal("updated@example.test", again.Player.Email);
        var separate = await Register("another-sub", "updated@example.test");
        Assert.NotEqual(id, separate.Player.Id);
        Assert.Equal(2, await fixture.Count("SELECT COUNT(*) FROM players"));
    }

    [IntegrationFact]
    public async Task Platform_credential_is_short_lived_and_player_cannot_administer_catalog()
    {
        await fixture.Reset();
        var registration = await Register("platform-sub");
        await using var scope = fixture.CreateScope();
        var credential = scope.ServiceProvider.GetRequiredService<IPlatformTokenIssuer>().Issue(registration.Player);
        Assert.Equal(900, credential.ExpiresIn);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(credential.AccessToken);
        Assert.Equal(registration.Player.Id.ToString(), jwt.Subject);
        Assert.Equal("Player", jwt.Claims.Single(x => x.Type == "role").Value);
        Assert.InRange((jwt.ValidTo - DateTime.UtcNow).TotalSeconds, 895, 900);
        using var client = fixture.AnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credential.AccessToken);
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var profile = await response.Content.ReadFromJsonAsync<PlayerProfile>();
        Assert.Equal(registration.Player.Id, profile!.Id); Assert.Equal(0, profile.Elo);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/cards")).StatusCode);
        await fixture.Execute($"UPDATE players SET active=FALSE WHERE id='{registration.Player.Id}'");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [IntegrationFact]
    public async Task Authentication_rejects_wrong_signature_audience_issuer_expiration_and_missing_token()
    {
        await fixture.Reset();
        var registration = await Register("rejection-sub");
        using var client = fixture.AnonymousClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        await using var scope = fixture.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<PlatformTokenOptions>();
        foreach (var variant in new[] { "signature", "audience", "issuer", "expired" })
        {
            var key = variant == "signature" ? RandomNumberGenerator.GetBytes(32) : config.GetSigningKey()!;
            var jwt = new JwtSecurityToken(variant == "issuer" ? "another-issuer" : config.Issuer,
                variant == "audience" ? "another-app" : config.Audience,
                [new Claim("sub", registration.Player.Id.ToString()), new Claim("role", "Player")],
                DateTime.UtcNow.AddMinutes(-20), DateTime.UtcNow.AddMinutes(variant == "expired" ? -1 : 5),
                new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        }
    }

    [IntegrationFact]
    public async Task Missing_google_configuration_is_explicit_and_does_not_create_accounts()
    {
        await fixture.Reset();
        var verifier = new GoogleIdentityVerifier(new GoogleIdentityOptions());
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => verifier.VerifyAsync("not-a-token", CancellationToken.None));
        Assert.Equal("google_not_configured", error.Code); Assert.Equal(503, error.StatusCode);
        using var client = fixture.AnonymousClient();
        var config = await client.GetFromJsonAsync<JsonElement>("/api/auth/config");
        Assert.False(config.TryGetProperty("signingKeyBase64", out _));
        var invalid = await client.PostAsJsonAsync("/api/auth/google", new { idToken = "not-a-token", role = "ContentAdmin", elo = 999 });
        Assert.Equal(config.GetProperty("enabled").GetBoolean() ? HttpStatusCode.Unauthorized : HttpStatusCode.ServiceUnavailable, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/google", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/auth/google", new { idToken = new string('x', 10001) })).StatusCode);
        Assert.Equal(0, await fixture.Count("SELECT COUNT(*) FROM players"));
    }

    [IntegrationFact]
    public async Task Real_google_verifier_rejects_malformed_tokens_without_mocking_certificates()
    {
        // Esta audiencia solo identifica el destino del caso negativo; no simula un token válido.
        var verifier = new GoogleIdentityVerifier(new GoogleIdentityOptions { ClientId = "invalid-client.apps.googleusercontent.com" });
        var error = await Assert.ThrowsAsync<AuthenticationFailure>(() => verifier.VerifyAsync("not-a-token", CancellationToken.None));
        Assert.Equal("invalid_google_token", error.Code); Assert.Equal(401, error.StatusCode);
    }

    [IntegrationFact]
    public async Task Login_migration_can_be_reapplied_without_losing_accounts()
    {
        await fixture.Reset();
        var registered = await Register("existing-account");
        var migration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "TestData/google_login.sql"));
        await fixture.Execute(migration);
        Assert.Equal(registered.Player.Id, (await Register("existing-account")).Player.Id);
        Assert.Equal(1, await fixture.Count("SELECT COUNT(*) FROM players"));
    }

    [LiveGoogleFact]
    public async Task Real_google_token_creates_player_and_returns_usable_platform_credentials()
    {
        await fixture.Reset();
        using var client = fixture.AnonymousClient();
        var body = new { idToken = Environment.GetEnvironmentVariable("POKEARENA_GOOGLE_ID_TOKEN") };
        var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => client.PostAsJsonAsync("/api/auth/google", body)));
        var sessions = new List<LoginResult>();
        foreach (var response in responses)
        {
            // No imprime cuerpos de respuesta para evitar exponer credenciales en resultados de pruebas.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            sessions.Add((await response.Content.ReadFromJsonAsync<LoginResult>())!);
        }
        Assert.Single(sessions, x => x.IsNewPlayer);
        Assert.Single(sessions.Select(x => x.Player.Id).Distinct());
        Assert.All(sessions, x => { Assert.Equal("Player", x.Player.Role); Assert.Equal(0, x.Player.Elo); });
        client.DefaultRequestHeaders.Authorization = new("Bearer", sessions[0].AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/cards")).StatusCode);
        Assert.Equal(1, await fixture.Count("SELECT COUNT(*) FROM players"));
        await fixture.Execute($"UPDATE players SET elo=125 WHERE id='{sessions[0].Player.Id}'");
        var repeat = await client.PostAsJsonAsync("/api/auth/google", body);
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        var repeated = await repeat.Content.ReadFromJsonAsync<LoginResult>();
        Assert.False(repeated!.IsNewPlayer); Assert.Equal(125, repeated.Player.Elo);
        Assert.Equal(sessions[0].Player.Id, repeated.Player.Id);
        client.DefaultRequestHeaders.Authorization = new("Bearer", body.idToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }
}
