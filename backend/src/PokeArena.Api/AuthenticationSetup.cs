using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using PokeArena.Application.Authentication;
using PokeArena.Infrastructure.Authentication;

namespace PokeArena.Api;

/// <summary>Separa los tokens propios de Google y del acceso administrativo local existente.</summary>
public static class AuthenticationSetup
{
    public static void AddPlatformAuthentication(this IServiceCollection services, IHostEnvironment environment)
    {
        // Resuelve la configuración al construir servicios, después de aplicar todas sus fuentes.
        services.AddSingleton(services => services.GetRequiredService<IConfiguration>()
            .GetSection("Authentication:Google").Get<GoogleIdentityOptions>() ?? new());
        services.AddSingleton(services => services.GetRequiredService<IConfiguration>()
            .GetSection("Authentication:Platform").Get<PlatformTokenOptions>() ?? new());
        services.AddSingleton<IGoogleIdentityVerifier, GoogleIdentityVerifier>();
        services.AddSingleton<IPlatformTokenIssuer, PlatformTokenIssuer>();
        services.AddScoped<IPlayerRepository, PostgresPlayerRepository>();
        services.AddScoped<LoginService>();

        var authentication = services.AddAuthentication(PlatformTokenOptions.Scheme)
            .AddJwtBearer(PlatformTokenOptions.Scheme);
        services.AddOptions<JwtBearerOptions>(PlatformTokenOptions.Scheme)
            .Configure<PlatformTokenOptions>((options, platform) =>
            {
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                // Sin clave configurada no hay una firma aceptable ni emisión de tokens.
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = platform.Issuer,
                    ValidateAudience = true, ValidAudience = platform.Audience,
                    ValidateLifetime = true, RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true, RequireSignedTokens = true,
                    IssuerSigningKey = platform.GetSigningKey() is { } key ? new SymmetricSecurityKey(key) : null,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.Zero, RoleClaimType = "role", NameClaimType = "sub"
                };
                options.Events = Events();
            });

        // Los JWT de dotnet user-jwts siguen limitados al catálogo en Development.
        if (environment.IsDevelopment())
            authentication.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters.ValidateIssuer = true;
                options.TokenValidationParameters.ValidateAudience = true;
                options.TokenValidationParameters.ValidateLifetime = true;
                options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                options.TokenValidationParameters.RoleClaimType = "role";
                options.TokenValidationParameters.NameClaimType = "sub";
                options.Events = Events();
            });

        services.AddAuthorization(options => options.AddPolicy("ContentAdmin", policy =>
        {
            policy.AddAuthenticationSchemes(PlatformTokenOptions.Scheme);
            if (environment.IsDevelopment()) policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser().RequireRole("ContentAdmin");
        }));
        services.AddRateLimiter(options =>
        {
            options.AddPolicy("google-login", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
                }));
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsJsonAsync(new
                    { type = "too_many_requests", message = "Demasiados intentos. Espere un minuto." }, token);
            };
        });
    }

    private static JwtBearerEvents Events() => new()
    {
        OnChallenge = context =>
        {
            context.HandleResponse();
            if (context.Response.HasStarted) return Task.CompletedTask;
            context.Response.StatusCode = 401;
            return context.Response.WriteAsJsonAsync(new
                { type = "authentication_required", message = "Debe iniciar sesión para continuar." });
        },
        OnForbidden = context =>
        {
            if (context.Response.HasStarted) return Task.CompletedTask;
            context.Response.StatusCode = 403;
            return context.Response.WriteAsJsonAsync(new
                { type = "access_denied", message = "La cuenta no tiene permisos para esta operación." });
        }
    };
}
