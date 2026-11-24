namespace PokeArena.Domain.Matchmaking;

/// <summary>Estado persistido de una partida, su preparación reproducible y el resultado final.</summary>
public sealed class GameMatch
{
    public Guid Id { get; set; }
    public required string Context { get; set; }
    public Guid? ContextId { get; set; }
    public string Status { get; set; } = MatchmakingRules.ActiveMatch;
    public DateTime CreatedAt { get; set; }
    public bool IsBot { get; set; }
    // El bot usa una copia del deck fijado al entrar, sin crear jugadores artificiales.
    public long[]? BotCardIds { get; set; }
    public long[]? BotHandCardIds { get; set; }
    public long[] BotCemeteryCardIds { get; set; } = [];
    public int BotEnergy { get; set; }
    /// <summary>Semilla privada del servidor para reproducir ubicaciones y barajados.</summary>
    public long PreparationSeed { get; set; }
    /// <summary>Turno actual de la partida; el límite jugable es el turno seis.</summary>
    public int TurnNumber { get; set; } = MatchmakingRules.InitialTurn;
    /// <summary>Instantes UTC de inicio y vencimiento del turno activo.</summary>
    public DateTime? TurnStartedAt { get; set; }
    public DateTime? TurnEndsAt { get; set; }
    public int WagerCubes { get; set; }
    public Guid? WinnerPlayerId { get; set; }
    public bool? IsDraw { get; set; }
    /// <summary>Ubicaciones seleccionadas junto con snapshots de su configuración al crear la partida.</summary>
    public List<MatchLocationChoice> Locations { get; set; } = [];
}

/// <summary>Estado privado de un participante: deck, mano, mazo restante, campo, energía y cementerio.</summary>
public sealed class MatchPlayer
{
    public Guid MatchId { get; set; }
    public Guid PlayerId { get; set; }
    public required string DeckId { get; set; }
    public required long[] CardIds { get; set; }
    public long[] RemainingCardIds { get; set; } = [];
    public int Elo { get; set; }
    public bool Active { get; set; } = true;
    public long[] HandCardIds { get; set; } = [];
    public long[] CemeteryCardIds { get; set; } = [];
    public long[] PlayedCardIds { get; set; } = [];
    public int[] PlayedLocationSlots { get; set; } = [];
    public int Energy { get; set; }
}

public sealed class MatchLocationChoice
{
    public Guid MatchId { get; set; }
    public int Slot { get; set; }
    public long LocationId { get; set; }
    public required string Snapshot { get; set; }
}

public sealed class MatchLocationEnergy
{
    public long LocationId { get; set; }
    public required string EnergyMode { get; set; }
    public int ExtraEnergy { get; set; }
}
