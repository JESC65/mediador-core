using Mediador.ApplicationCore.Dtos;

namespace Mediador.ApplicationCore.Interfaces;

public interface ITransportistaCotizableRepository
{
    Task<IReadOnlyList<int>> ObtenerIdsConConvenioActivo(int idCourier);
    Task<IReadOnlyList<TransportistaCandidataDto>> ObtenerCandidatasConRuta(int idCourier, string? ubigeoOrigen, string ubigeoDestino);
    Task<TarifaTramoDto?> BuscarTramo(int idRuta, string tipoCarga, decimal pesoTotalKg);
}
