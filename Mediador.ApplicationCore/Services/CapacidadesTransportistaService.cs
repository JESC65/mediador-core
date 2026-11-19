using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Interfaces;
using Microsoft.Extensions.Logging;

namespace Mediador.ApplicationCore.Services;

public class CapacidadesTransportistaService : ICapacidadesTransportistaService
{
    private readonly ICapacidadesTransportistaRepository _repository;
    private readonly ISimuladorTransportistaClient _simuladorClient;
    private readonly IPuntosRecepcionService _puntosService;
    private readonly ILogger<CapacidadesTransportistaService> _logger;

    public CapacidadesTransportistaService(
        ICapacidadesTransportistaRepository repository,
        ISimuladorTransportistaClient simuladorClient,
        IPuntosRecepcionService puntosService,
        ILogger<CapacidadesTransportistaService> logger)
    {
        _repository = repository;
        _simuladorClient = simuladorClient;
        _puntosService = puntosService;
        _logger = logger;
    }

    public async Task<ResultadoSincronizacionCapacidadesDto> SincronizarTransportistaAsync(int idTransportista)
    {
        var transportista = await _repository.ObtenerTransportistaAsync(idTransportista);
        if (transportista is null)
        {
            _logger.LogWarning("No se encontró transportista con Id {IdTransportista} para sincronizar capacidades.", idTransportista);
            return ResultadoSincronizacionCapacidadesDto.NoEncontrado($"La transportista {idTransportista} no existe.");
        }

        if (string.IsNullOrWhiteSpace(transportista.UrlConector))
        {
            _logger.LogWarning("Transportista {IdTransportista} no tiene UrlConector configurada.", idTransportista);
            return ResultadoSincronizacionCapacidadesDto.Falla("La transportista no tiene UrlConector configurada.");
        }

        var resultado = await _simuladorClient.ObtenerCapacidades(transportista.UrlConector);
        if (!resultado.Exitosa || resultado.Respuesta is null)
        {
            _logger.LogWarning("Fallo al obtener capacidades de transportista {IdTransportista} ({UrlConector}): {Motivo}",
                idTransportista, transportista.UrlConector, resultado.ErrorComunicacion);
            return ResultadoSincronizacionCapacidadesDto.Falla(resultado.ErrorComunicacion ?? "ERROR_CONECTOR");
        }

        await _repository.GuardarCapacidadesAsync(idTransportista, resultado.Respuesta);
        _logger.LogInformation("Capacidades sincronizadas para transportista {IdTransportista} ({RazonSocial}).",
            idTransportista, transportista.RazonSocial);

        try
        {
            await _puntosService.SincronizarPuntosTransportistaAsync(idTransportista);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallo al sincronizar puntos para transportista {IdTransportista} tras sincronizar capacidades.", idTransportista);
        }

        return ResultadoSincronizacionCapacidadesDto.Ok(resultado.Respuesta);
    }

    public async Task SincronizarTodasActivasAsync()
    {
        IReadOnlyList<TransportistaInfoCapacidadesDto> activas;
        try
        {
            activas = await _repository.ObtenerActivasConUrlAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo consultar la lista de transportistas activas para sincronizar capacidades.");
            return;
        }

        _logger.LogInformation("Iniciando sincronización de capacidades para {Cantidad} transportista(s) activa(s)...", activas.Count);

        foreach (var t in activas)
        {
            try
            {
                var resultado = await SincronizarTransportistaAsync(t.IdTransportista);
                if (!resultado.Exitoso)
                {
                    _logger.LogWarning("No se pudieron sincronizar las capacidades de transportista {IdTransportista} ({RazonSocial}): {Detalle}",
                        t.IdTransportista, t.RazonSocial, resultado.Detalle);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Excepción al sincronizar capacidades de transportista {IdTransportista} ({RazonSocial}).",
                    t.IdTransportista, t.RazonSocial);
            }
        }

        _logger.LogInformation("Finalizó la sincronización de capacidades de transportistas activas.");
    }
}
