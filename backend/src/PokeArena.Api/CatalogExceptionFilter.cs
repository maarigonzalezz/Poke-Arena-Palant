using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PokeArena.Application.Common;
using PokeArena.Application.Authentication;
using PokeArena.Application.Inventory;

namespace PokeArena.Api;

/// <summary>Traduce fallos conocidos sin devolver SQL ni información de conexión al cliente.</summary>
public sealed class CatalogExceptionFilter(ILogger<CatalogExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        // Convierte excepciones conocidas en respuestas que el frontend puede interpretar.
        context.Result = context.Exception switch
        {
            AuthenticationFailure ex => new ObjectResult(new { type = ex.Code, message = ex.Message }) { StatusCode = ex.StatusCode },
            InventoryValidationException ex => new BadRequestObjectResult(new { type = ex.Type, message = ex.Message, errors = ex.Errors }),
            CatalogValidationException ex when ex.Type is not null =>
                new BadRequestObjectResult(new { type=ex.Type, message=ex.Message, errors=ex.Errors }),
            CatalogValidationException ex => new BadRequestObjectResult(new { message=ex.Message, errors=ex.Errors }),
            CatalogConflictException ex => new ConflictObjectResult(new { message=ex.Message }),
            DbUpdateConcurrencyException => new ConflictObjectResult(new { message="Los datos cambiaron durante la operación. Intente nuevamente." }),
            DbUpdateException ex when ex.InnerException is NpgsqlException =>
                new ObjectResult(new { message="No se pudo completar la operación de base de datos. Intente nuevamente." }) { StatusCode=503 },
            NpgsqlException => new ObjectResult(new { message="No se pudo completar la operación de base de datos. Intente nuevamente." }) { StatusCode=503 },
            IOException => new ObjectResult(new { message="No se pudo guardar la imagen. Intente nuevamente." }) { StatusCode=503 },
            _ => null
        };
        if (context.Result is not null)
        {
            if (context.Exception is NpgsqlException or DbUpdateException or IOException) logger.LogError(context.Exception, "Fallo al guardar o consultar el catálogo.");
            // Marca el error como atendido para usar la respuesta construida por el filtro.
            context.ExceptionHandled = true;
        }
    }
}
