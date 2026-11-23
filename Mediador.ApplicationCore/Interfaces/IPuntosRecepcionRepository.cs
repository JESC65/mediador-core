using Mediador.ApplicationCore.Dtos;

namespace Mediador.ApplicationCore.Interfaces;

public interface IPuntosRecepcionRepository
{
    Task<TransportistaInfoCapacidadesDto?> ObtenerTransportistaAsync(int idTransportista);
    Task<bool?> ObtenerCapacidadPuntosAsync(int idTransportista);
    Task GuardarPuntosAsync(int idTransportista, IReadOnlyList<PuntoRecepcionDto> puntos);
}
