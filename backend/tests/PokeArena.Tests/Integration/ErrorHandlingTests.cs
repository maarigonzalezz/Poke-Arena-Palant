using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace PokeArena.Tests.Integration;

// El controlador de prueba solo se registra en WebApplicationFactory, nunca en la API productiva.
[ApiController]
[Route("__tests/unexpected-error")]
public sealed class FailureProbeController : ControllerBase
{
    [HttpGet]
    public IActionResult Fail() => throw new InvalidOperationException("private-diagnostic-details");
}

[Collection("catalog-integration")]
[Trait("Category", "Integration")]
public sealed class ErrorHandlingTests(CatalogFixture fixture)
{
    [IntegrationFact]
    public async Task Unexpected_errors_return_safe_500_and_leave_the_api_available()
    {
        using var client = fixture.Client(null);
        var response = await client.GetAsync("/__tests/unexpected-error");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("internal_error", text);
        Assert.Contains("traceId", text);
        Assert.DoesNotContain("private-diagnostic-details", text);
        Assert.DoesNotContain("InvalidOperationException", text);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
    }
}
