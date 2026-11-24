using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PokeArena.Application.Matchmaking;
using PokeArena.Infrastructure.Authentication;

namespace PokeArena.Api.Controllers;

// HTTP transporta el identificador del deck; jugador, Elo y saldo proceden del servidor.
[ApiController]
[Route("api/player/matchmaking")]
[Authorize(AuthenticationSchemes = PlatformTokenOptions.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MatchmakingController(MatchmakingService matchmaking) : ControllerBase
{
    [HttpPost("ranked")]
    public async Task<IActionResult> Start([FromBody] StartMatchSearchRequest request, CancellationToken token)
    {
        if (!TryPlayer(out var id)) return Unauthorized();
        return Ok(await matchmaking.StartRankedAsync(id, request.DeckId, token));
    }

    [HttpGet("current")]
    public async Task<IActionResult> Current(CancellationToken token)
    {
        if (!TryPlayer(out var id)) return Unauthorized();
        var search = await matchmaking.CurrentAsync(id, token);
        return search is null ? NoContent() : Ok(search);
    }

    [HttpDelete("{searchId:guid}")]
    public async Task<IActionResult> Cancel(Guid searchId, CancellationToken token)
    {
        if (!TryPlayer(out var id)) return Unauthorized();
        return Ok(await matchmaking.CancelAsync(id, searchId, token));
    }

    [HttpGet("decks")]
    public async Task<IActionResult> Decks(CancellationToken token)
    {
        if (!TryPlayer(out var id)) return Unauthorized();
        return Ok(await matchmaking.DecksAsync(id, token));
    }

    [HttpPost("{searchId:guid}/bot")]
    public async Task<IActionResult> AcceptBot(Guid searchId, CancellationToken token)
    {
        if (!TryPlayer(out var id)) return Unauthorized();
        return Ok(await matchmaking.AcceptBotAsync(id, searchId, token));
    }

    private bool TryPlayer(out Guid id) => Guid.TryParse(User.FindFirst("sub")?.Value, out id) && id != Guid.Empty;
}
