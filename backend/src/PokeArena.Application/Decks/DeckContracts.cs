using System.Text.Json.Serialization;

namespace PokeArena.Application.Decks;

public sealed record CreateDeckRequest(string? Name, IReadOnlyList<string?>? CardCodes);
public sealed record DeckResult(Guid Id, string Name, IReadOnlyList<string> CardCodes, DateTime CreatedAt)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<DeckCardPreview>? Cards { get; init; }
}

public sealed record DeckCardPreview(
    string Code, string Name, string Type, int Cost, int Power, string? ImageUrl, string? ImageSmallUrl);

public sealed record OwnedDeckCard(long CardId, string Code);

public sealed class DeckFailure(string code, string message, int statusCode, object? details = null) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public object? Details { get; } = details;
}
