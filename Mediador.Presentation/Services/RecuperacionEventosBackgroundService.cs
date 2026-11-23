using Mediador.ApplicationCore.Interfaces;
using Mediador.ApplicationCore.Utilidades;
using Mediador.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mediador.Presentation.Services;

public class RecuperacionEventosBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecuperacionEventosBackgroundService> _logger;
    private readonly INotificadorHuecoSecuencia _notificadorHueco;
    private readonly ParametrosOptions _parametros;

    public RecuperacionEventosBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<RecuperacionEventosBackgroundService> logger,
        INotificadorHuecoSecuencia notificadorHueco,
        IOptions<ParametrosOptions> parametros)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _notificadorHueco = notificadorHueco;
        _parametros = parametros.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RecuperacionEventosBackgroundService iniciado. Intervalo t_recuperacion: {Intervalo}", _parametros.t_recuperacion);

        // 1. Ejecución al arrancar la aplicación (Bloque 3)
        try
        {
            _logger.LogInformation("Ejecutando recuperación inicial al arrancar el Mediador...");
            await EjecutarRecuperacionAsync(stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Error durante la recuperación inicial al arranque.");
        }

        // 2 y 3. Periódicamente cada t_recuperacion o inmediatamente ante hueco (por canal)
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var ctsIteracion = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var tareaTimer = Task.Delay(_parametros.t_recuperacion, ctsIteracion.Token);
                var tareaHueco = _notificadorHueco.Lector.WaitToReadAsync(ctsIteracion.Token).AsTask();

                var tareaCompletada = await Task.WhenAny(tareaTimer, tareaHueco);

                try
                {
                    if (tareaCompletada == tareaHueco && await tareaHueco)
                    {
                        while (_notificadorHueco.Lector.TryRead(out _)) { }
                        _logger.LogInformation("Hueco de secuencia detectado: ejecutando recuperación inmediata.");
                    }
                    else
                    {
                        _logger.LogInformation("Período t_recuperacion cumplido: ejecutando ciclo de recuperación programado.");
                    }
                }
                finally
                {
                    ctsIteracion.Cancel();
                }

                await EjecutarRecuperacionAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ciclo de recuperación de eventos.");
            }
        }
    }

    public async Task EjecutarRecuperacionAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediadorDbContext>();
        var eventoRepo = scope.ServiceProvider.GetRequiredService<IEventoRepository>();
        var eventoService = scope.ServiceProvider.GetRequiredService<IEventoMediadorService>();
        var clienteSimulador = scope.ServiceProvider.GetRequiredService<ISimuladorTransportistaClient>();

        var transportistas = await db.Transportistas
            .AsNoTracking()
            .Where(t => t.Activo && !string.IsNullOrWhiteSpace(t.UrlConector))
            .ToListAsync(stoppingToken);

        foreach (var transportista in transportistas)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                var desdeSecuencia = await eventoRepo.ObtenerUltimaSecuenciaContigua(transportista.IdTransportista);
                _logger.LogInformation(
                    "Consultando recuperación B7 para transportista {Id} ({Nombre}) desde secuencia {Secuencia}...",
                    transportista.IdTransportista, transportista.NombreComercial ?? transportista.RazonSocial, desdeSecuencia);

                var resultado = await clienteSimulador.RecuperarEventos(transportista.UrlConector!, desdeSecuencia);

                if (!resultado.Exitosa || resultado.Respuesta is null)
                {
                    _logger.LogWarning(
                        "No se pudieron recuperar eventos para transportista {Id}: {Motivo}",
                        transportista.IdTransportista, resultado.ErrorComunicacion);
                    continue;
                }

                var eventosRecuperados = resultado.Respuesta
                    .OrderBy(e => e.Secuencia ?? int.MaxValue)
                    .ToList();

                _logger.LogInformation(
                    "Transportista {Id}: {Count} evento(s) recuperado(s). Procesando en orden de secuencia...",
                    transportista.IdTransportista, eventosRecuperados.Count);

                foreach (var evento in eventosRecuperados)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    await eventoService.ProcesarEvento(evento);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error recuperando eventos para transportista {Id}", transportista.IdTransportista);
            }
        }
    }
}
