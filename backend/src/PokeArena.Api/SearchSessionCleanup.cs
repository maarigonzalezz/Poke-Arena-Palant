using Npgsql;

namespace PokeArena.Api;

/// <summary>Elimina sesiones vencidas; sus resultados se eliminan por la relación en cascada.</summary>
public sealed class SearchSessionCleanup(NpgsqlDataSource dataSource, ILogger<SearchSessionCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var command = dataSource.CreateCommand("DELETE FROM card_search_sessions WHERE expires_at <= NOW()");
                    await command.ExecuteNonQueryAsync(stoppingToken);
                }
                catch (NpgsqlException ex) { logger.LogWarning(ex, "No se pudieron limpiar las sesiones vencidas."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
