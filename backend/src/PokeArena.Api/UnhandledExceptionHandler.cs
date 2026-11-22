using Microsoft.AspNetCore.Diagnostics;

namespace PokeArena.Api;

// Centraliza fallos inesperados; las validaciones conocidas conservan su código HTTP.
public sealed class UnhandledExceptionHandler(ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (context.RequestAborted.IsCancellationRequested) return false;
        logger.LogError(exception, "Fallo inesperado en {Method} {Path}.", context.Request.Method, context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new {
            type = "internal_error",
            message = "Ocurrió un error inesperado. Intente nuevamente.",
            traceId = context.TraceIdentifier
        }, cancellationToken);
        return true;
    }
}
