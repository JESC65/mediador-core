using Mediador.ApplicationCore.Dtos;

namespace Mediador.ApplicationCore.Interfaces;

public interface IRutaTarifaEventoRepository
{
    Task ActualizarOCrearTarifa(int idTransportista, PrecioActualizadoDto datos);
    Task ActualizarOCrearRuta(int idTransportista, RutaEventoDto datos);
}
