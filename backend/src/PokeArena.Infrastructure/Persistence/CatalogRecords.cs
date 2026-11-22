namespace PokeArena.Infrastructure.Persistence;

// Estas entidades internas representan tablas auxiliares sin introducir EF en el dominio.
internal sealed class AbilityRecord
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? TriggerType { get; set; }
    public string? EffectType { get; set; }
    public string Parameters { get; set; } = "{}";
    public string? RestrictedCardType { get; set; }
    public bool Active { get; set; }
}

// El contador permanece bloqueado hasta confirmar o revertir la creación.
internal sealed class CardCodeSequence
{
    public string CardType { get; set; } = "";
    public int LastNumber { get; set; }
}

// Cada versión conserva el contrato publicado, serializado como JSONB.
internal sealed class CardVersion
{
    public long CardId { get; set; }
    public int Version { get; set; }
    public string Snapshot { get; set; } = "{}";
}

internal sealed class LocationVersion
{
    public long LocationId { get; set; }
    public int Version { get; set; }
    public string Snapshot { get; set; } = "{}";
}

// La combinación actor, operación y clave identifica un único reintento.
internal sealed class IdempotencyRequest
{
    public long Id { get; set; }
    public string ActorId { get; set; } = "";
    public string Operation { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string Status { get; set; } = "Processing";
    public int? ResponseStatus { get; set; }
    public string? ResponseBody { get; set; }
}

// La sesión fija el conjunto de cartas y pertenece a un solo administrador.
internal sealed class CardSearchSession
{
    public Guid Id { get; set; }
    public string ActorId { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public long ResultCount { get; set; }
}

internal sealed class CardSearchEntry
{
    public Guid SessionId { get; set; }
    public long Position { get; set; }
    public long CardId { get; set; }
}
