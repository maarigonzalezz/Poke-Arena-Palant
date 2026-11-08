using PokeArena.Application.Authentication;
using PokeArena.Application.Cards;

namespace PokeArena.Application.Inventory;


// El servidor informa si la entrega terminó o debe intentarse en otro ingreso.
public sealed record InitialCollectionResult(string Status, string? Message = null)
{
    public bool Completed => Status == "Completed";
}

public sealed record CubeMovementItem(int Sequence, int Amount, int BalanceAfter, string Reason, DateTime CreatedAt);
public sealed record CubeHistoryResult(IReadOnlyList<CubeMovementItem> Items, int? NextSequence);

/// <summary>Resultado de consulta. Perfil del jugador con su inventario actual</summary>
public sealed record PlayerProfileResult (
    PlayerProfile Profile,
    bool NewPlayer,
    IEnumerable<CardItem> Collection,
    InitialCollectionResult InitialCollection,
    IReadOnlyList<long> SuggestedDeckCardIds
);
