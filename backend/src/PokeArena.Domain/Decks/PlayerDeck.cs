using System.Diagnostics.CodeAnalysis;
using PokeArena.Domain.Catalogs;

namespace PokeArena.Domain.Decks;

public sealed class PlayerDeck
{
    public const int MaximumNameLength = 40;

    public Guid Id { get; }
    public Guid PlayerId { get; }
    public string Name { get; }
    public DateTime CreatedAt { get; }
    public IReadOnlyList<long> CardIds { get; }

    public static bool IsValidName([NotNullWhen(true)] string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= MaximumNameLength && name == name.Trim() && !name.Contains('\0');

    public PlayerDeck(Guid playerId, string name, DateTime createdAt, IReadOnlyList<long> cardIds)
    {
        if (!IsValidName(name))
            throw new ArgumentException("El nombre del mazo debe ser válido y estar normalizado.", nameof(name));
        if (cardIds.Count != GameRules.DeckSize || cardIds.Distinct().Count() != GameRules.DeckSize)
            throw new ArgumentException($"El mazo debe tener {GameRules.DeckSize} cartas distintas.", nameof(cardIds));

        Id = Guid.NewGuid();
        PlayerId = playerId;
        Name = name;
        CreatedAt = createdAt;
        CardIds = Array.AsReadOnly(cardIds.ToArray());
    }
}
