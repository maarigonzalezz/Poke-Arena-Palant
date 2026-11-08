namespace PokeArena.Domain.Entities;

/// <summary>Cuenta local identificada por proveedor y sub; el correo no identifica la cuenta.</summary>
public sealed class Player
{
    public Guid Id { get; set; }
    public required string IdentityProvider { get; set; }
    public required string ProviderSubject { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string Role { get; set; } = "Player";
    public int Elo { get; set; }
    public int Cubes {get; set; }
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime LastLoginAt { get; set; }
}
