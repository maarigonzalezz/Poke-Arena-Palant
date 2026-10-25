using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Npgsql;
using PokeArena.Application.Common;

namespace PokeArena.Api;

/// <summary>Traduce fallos conocidos sin devolver SQL ni información de conexión al cliente.</summary>
public sealed class CatalogExceptionFilter(ILogger<CatalogExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        context.Result = context.Exception switch
        {
            CatalogValidationException ex => new BadRequestObjectResult(new { message=ex.Message, errors=ex.Errors }),
            CatalogConflictException ex => new ConflictObjectResult(new { message=ex.Message }),
            NpgsqlException => new ObjectResult(new { message="No se pudo completar la operación de base de datos. Intente nuevamente." }) { StatusCode=503 },
            IOException => new ObjectResult(new { message="No se pudo guardar la imagen. Intente nuevamente." }) { StatusCode=503 },
            _ => null
        };
        if (context.Result is not null)
        {
            if (context.Exception is NpgsqlException or IOException) logger.LogError(context.Exception, "Fallo al guardar o consultar el catálogo.");
            context.ExceptionHandled = true;
        }
    }
}
