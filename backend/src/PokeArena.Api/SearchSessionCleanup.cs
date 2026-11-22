using Npgsql;
using PokeArena.Infrastructure.Persistence;

namespace PokeArena.Api;

/// <summary>Elimina sesiones vencidas; sus resultados se eliminan por la relación en cascada.</summary>
public sealed class SearchSessionCleanup(IServiceScopeFactory scopes, ILogger<SearchSessionCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // La limpieza periódica evita conservar indefinidamente resultados de búsquedas vencidas.
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    // Cada pasada obtiene su contexto; un servicio singleton no comparte DbContext.
                    await using var scope = scopes.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<PokeArenaDbContext>();
                    await db.DeleteExpiredSearchSessionsAsync(stoppingToken);
                }
                catch (NpgsqlException ex) { logger.LogWarning(ex, "No se pudieron limpiar las sesiones vencidas."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
