using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PokeArena.Application.Matchmaking;
using PokeArena.Infrastructure.Authentication;

namespace PokeArena.Api.Controllers;

[ApiController]
[Route("api/player/decks")]
[Authorize(AuthenticationSchemes = PlatformTokenOptions.Scheme)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PlayerDecksController(PlayerDeckService decks) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePlayerDeckRequest request, CancellationToken token)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var playerId) || playerId == Guid.Empty)
            return Unauthorized();
        return Ok(await decks.CreateAsync(playerId, request, token));
    }
}
