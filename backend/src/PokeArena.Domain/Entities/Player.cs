using PokeArena.Domain.Catalogs;

namespace PokeArena.Domain.Entities;

/// <summary>Cuenta local identificada por proveedor y sub; el correo no identifica la cuenta.</summary>
public sealed class Player
{
    public Guid Id { get; set; }
    public required string IdentityProvider { get; set; }
    public required string ProviderSubject { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string Role { get; set; } = GameRules.PlayerRole;
    public int Elo { get; set; }
    public int Cubes {get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime LastLoginAt { get; set; }

    // El dominio define los valores iniciales; el repositorio únicamente persiste la cuenta.
    public static Player Create(string provider, string subject, string? email, string? displayName) => new()
    {
        Id = Guid.NewGuid(), IdentityProvider = provider, ProviderSubject = subject,
        Email = email, DisplayName = displayName, Role = GameRules.PlayerRole,
        Elo = GameRules.InitialElo, Cubes = 0, Active = true,
        CreatedAt = DateTime.UtcNow, LastLoginAt = DateTime.UtcNow
    };

    // La identidad externa puede actualizar el perfil, pero nunca permisos, Elo ni saldo.
    public void RecordLogin(string? email, string? displayName)
    {
        if (!Active) return;
        Email = email;
        DisplayName = displayName;
        LastLoginAt = DateTime.UtcNow;
    }
}
