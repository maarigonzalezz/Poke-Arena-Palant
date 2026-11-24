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
    /// <summary>Abre o recupera la búsqueda clasificatoria asociada al mazo elegido.</summary>
    [HttpPost("ranked")]
    public async Task<IActionResult> Start([FromBody] StartMatchSearchRequest request, CancellationToken token)
    {
        if (!TryPlayer(out var id)) return Unauthorized();
        return Ok(await matchmaking.StartRankedAsync(id, request.DeckId, token));
    }

    /// <summary>Devuelve la búsqueda más reciente del jugador y renueva su presencia si sigue abierta.</summary>
    [HttpGet("current")]
    public async Task<IActionResult> Current(CancellationToken token)
    {
        if (!TryPlayer(out var id)) return Unauthorized();
        var search = await matchmaking.CurrentAsync(id, token);
        return search is null ? NoContent() : Ok(search);
    }

    /// <summary>Cancela una búsqueda abierta que pertenece al jugador autenticado.</summary>
    [HttpDelete("{searchId:guid}")]
    public async Task<IActionResult> Cancel(Guid searchId, CancellationToken token)
    {
        if (!TryPlayer(out var id)) return Unauthorized();
        return Ok(await matchmaking.CancelAsync(id, searchId, token));
    }

    /// <summary>Lista los mazos propios que se pueden seleccionar para buscar partida.</summary>
    [HttpGet("decks")]
    public async Task<IActionResult> Decks(CancellationToken token)
    {
        if (!TryPlayer(out var id)) return Unauthorized();
        return Ok(await matchmaking.DecksAsync(id, token));
    }

    /// <summary>Acepta la oferta de rival bot cuando la búsqueda clasificatoria ya es elegible.</summary>
    [HttpPost("{searchId:guid}/bot")]
    public async Task<IActionResult> AcceptBot(Guid searchId, CancellationToken token)
    {
        if (!TryPlayer(out var id)) return Unauthorized();
        return Ok(await matchmaking.AcceptBotAsync(id, searchId, token));
    }

    /// <summary>Obtiene el estado de una partida en la que participa el jugador autenticado.</summary>
    [HttpGet("{matchId:guid}/state")]
    public async Task<IActionResult> MatchState(Guid matchId, CancellationToken token)
    {
        if (!TryPlayer(out var id)) return Unauthorized();
        var state = await matchmaking.MatchStateAsync(id, matchId, token);
        return state is null ? NotFound() : Ok(state);
    }

    /// <summary>Avanza un turno vencido; un número de turno opcional hace idempotentes los reintentos antiguos.</summary>
    [HttpPost("{matchId:guid}/turn/advance")]
    public async Task<IActionResult> AdvanceTurn(Guid matchId, [FromBody] AdvanceMatchTurnRequest? request, CancellationToken token)
    {
        if (!TryPlayer(out var id)) return Unauthorized();
        var state = await matchmaking.AdvanceMatchAsync(id, matchId, request?.TurnNumber, token);
        return state is null ? NotFound() : Ok(state);
    }

    /// <summary>Juega una carta de la mano del jugador en una ubicación de la partida.</summary>
    [HttpPost("{matchId:guid}/cards/play")]
    public async Task<IActionResult> PlayCard(Guid matchId, [FromBody] PlayMatchCardRequest request, CancellationToken token)
    {
        if (!TryPlayer(out var id)) return Unauthorized();
        var state = await matchmaking.PlayCardAsync(id, matchId, request.CardId, request.LocationSlot, token);
        return state is null ? NotFound() : Ok(state);
    }

    /// <summary>Descarta una carta de la mano del jugador y la mueve a su cementerio.</summary>
    [HttpPost("{matchId:guid}/cards/discard")]
    public async Task<IActionResult> DiscardCard(Guid matchId, [FromBody] DiscardMatchCardRequest request, CancellationToken token)
    {
        if (!TryPlayer(out var id)) return Unauthorized();
        var state = await matchmaking.DiscardCardAsync(id, matchId, request.CardId, token);
        return state is null ? NotFound() : Ok(state);
    }

    private bool TryPlayer(out Guid id) => Guid.TryParse(User.FindFirst("sub")?.Value, out id) && id != Guid.Empty;
}
