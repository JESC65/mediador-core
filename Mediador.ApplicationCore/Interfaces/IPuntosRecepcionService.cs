using Mediador.ApplicationCore.Dtos;

namespace Mediador.ApplicationCore.Interfaces;

public interface IPuntosRecepcionService
{
    Task<ResultadoSincronizacionPuntosDto> SincronizarPuntosTransportistaAsync(int idTransportista);
}
