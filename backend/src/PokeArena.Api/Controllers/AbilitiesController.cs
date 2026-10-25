using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PokeArena.Application.Cards;

namespace PokeArena.Api.Controllers;

[ApiController]
[Route("api/abilities")]
[Authorize(Roles = "ContentAdmin")]
public sealed class AbilitiesController(IAbilityRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await repository.GetAsync(cancellationToken));
}
