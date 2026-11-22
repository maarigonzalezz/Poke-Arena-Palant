using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using PokeArena.Application.Authentication;
using PokeArena.Domain.Entities;

namespace PokeArena.Infrastructure.Authentication;

/// <summary>Emite JWT propios sin copiar permisos del token externo ni almacenar credenciales.</summary>
public sealed class PlatformTokenIssuer(PlatformTokenOptions options) : IPlatformTokenIssuer
{
    public void EnsureConfigured() => options.EnsureConfigured();

    public AccessCredential Issue(Player player)
    {
        EnsureConfigured();
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(options.LifetimeMinutes);
        var token = new JwtSecurityToken(options.Issuer, options.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, player.Id.ToString()),
             new Claim("role", player.Role),
             new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
             new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(now).ToString(), ClaimValueTypes.Integer64)],
            now, expires, new SigningCredentials(new SymmetricSecurityKey(options.GetSigningKey()!), SecurityAlgorithms.HmacSha256));
        return new AccessCredential(new JwtSecurityTokenHandler().WriteToken(token), "Bearer",
            options.LifetimeMinutes * 60, expires);
    }
}
