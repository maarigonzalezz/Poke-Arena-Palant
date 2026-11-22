using PokeArena.Application.Matchmaking;
using PokeArena.Domain.Matchmaking;

namespace PokeArena.Api;

// Revisa la cola aunque nadie haga otra petición; varias instancias comparten el bloqueo SQL.
public sealed class MatchmakingWorker(IServiceScopeFactory scopes, ILogger<MatchmakingWorker> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Consulta la configuración final, incluidas las opciones del host de pruebas.
        if (!configuration.GetValue("Matchmaking:RunWorker", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(MatchmakingRules.ProcessingIntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<MatchmakingService>().ProcessAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception ex) { logger.LogError(ex, "No se pudo procesar la cola; se reintentará en el próximo ciclo."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
