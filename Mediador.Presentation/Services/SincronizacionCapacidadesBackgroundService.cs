using Mediador.ApplicationCore.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mediador.Presentation.Services;

public class SincronizacionCapacidadesBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SincronizacionCapacidadesBackgroundService> _logger;

    public SincronizacionCapacidadesBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<SincronizacionCapacidadesBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SincronizacionCapacidadesBackgroundService iniciado. Ejecutando sincronización de capacidades al arranque...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ICapacidadesTransportistaService>();
            await service.SincronizarTodasActivasAsync();
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Error durante la sincronización de capacidades al arranque.");
        }
    }
}
