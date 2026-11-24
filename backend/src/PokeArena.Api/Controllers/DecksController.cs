using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PokeArena.Application.Decks;
using PokeArena.Infrastructure.Authentication;

namespace PokeArena.Api.Controllers;

[ApiController]
[Route("api/player/decks")]
[Authorize(AuthenticationSchemes = PlatformTokenOptions.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DecksController(DeckService decks) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken token)
    {
        if (!TryPlayer(out var playerId)) return Unauthorized();
        return Ok(await decks.ListAsync(playerId, token));
    }

    [HttpGet("{deckId:guid}")]
    public async Task<IActionResult> Find(Guid deckId, CancellationToken token)
    {
        if (!TryPlayer(out var playerId)) return Unauthorized();
        return Ok(await decks.FindAsync(playerId, deckId, token));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateDeckRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken token)
    {
        if (!TryPlayer(out var playerId)) return Unauthorized();
        var deck = await decks.CreateAsync(playerId, key, request, token);
        return CreatedAtAction(nameof(Find), new { deckId = deck.Id }, deck);
    }

    private bool TryPlayer(out Guid id) => Guid.TryParse(User.FindFirst("sub")?.Value, out id) && id != Guid.Empty;
}
