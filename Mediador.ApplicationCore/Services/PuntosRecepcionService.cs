using System;
using System.Threading.Tasks;
using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Interfaces;
using Microsoft.Extensions.Logging;

namespace Mediador.ApplicationCore.Services;

public class PuntosRecepcionService : IPuntosRecepcionService
{
    private readonly IPuntosRecepcionRepository _repository;
    private readonly ISimuladorTransportistaClient _simuladorClient;
    private readonly ILogger<PuntosRecepcionService> _logger;

    public PuntosRecepcionService(
        IPuntosRecepcionRepository repository,
        ISimuladorTransportistaClient simuladorClient,
        ILogger<PuntosRecepcionService> logger)
    {
        _repository = repository;
        _simuladorClient = simuladorClient;
        _logger = logger;
    }

    public async Task<ResultadoSincronizacionPuntosDto> SincronizarPuntosTransportistaAsync(int idTransportista)
    {
        var transportista = await _repository.ObtenerTransportistaAsync(idTransportista);
        if (transportista is null)
        {
            _logger.LogWarning("No se encontró transportista con Id {IdTransportista} para sincronizar puntos.", idTransportista);
            return ResultadoSincronizacionPuntosDto.NoEncontrado($"La transportista {idTransportista} no existe.");
        }

        var capacidadPuntos = await _repository.ObtenerCapacidadPuntosAsync(idTransportista);
        if (capacidadPuntos == false)
        {
            _logger.LogInformation("Transportista {IdTransportista} ({RazonSocial}) declara Puntos == false. Se omite consulta B6.",
                idTransportista, transportista.RazonSocial);
            return ResultadoSincronizacionPuntosDto.NoSoportado();
        }

        if (string.IsNullOrWhiteSpace(transportista.UrlConector))
        {
            _logger.LogWarning("Transportista {IdTransportista} ({RazonSocial}) no tiene UrlConector configurada para B6.",
                idTransportista, transportista.RazonSocial);
            return ResultadoSincronizacionPuntosDto.Falla("La transportista no tiene UrlConector configurada.");
        }

        var resultado = await _simuladorClient.ObtenerPuntos(transportista.UrlConector);
        if (!resultado.Exitosa || resultado.Respuesta is null)
        {
            _logger.LogWarning("Fallo al consultar puntos B6 de transportista {IdTransportista} ({UrlConector}): {Motivo}",
                idTransportista, transportista.UrlConector, resultado.ErrorComunicacion);
            return ResultadoSincronizacionPuntosDto.Falla(resultado.ErrorComunicacion ?? "ERROR_CONECTOR");
        }

        var puntos = resultado.Respuesta.Puntos ?? new List<PuntoRecepcionDto>();
        await _repository.GuardarPuntosAsync(idTransportista, puntos);
        _logger.LogInformation("Se sincronizaron exitosamente {Count} puntos para transportista {IdTransportista} ({RazonSocial}).",
            puntos.Count, idTransportista, transportista.RazonSocial);

        return ResultadoSincronizacionPuntosDto.Ok(puntos.Count);
    }
}
