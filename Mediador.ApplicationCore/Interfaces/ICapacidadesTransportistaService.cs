using Mediador.ApplicationCore.Dtos;

namespace Mediador.ApplicationCore.Interfaces;

public interface ICapacidadesTransportistaService
{
    Task<ResultadoSincronizacionCapacidadesDto> SincronizarTransportistaAsync(int idTransportista);
    Task SincronizarTodasActivasAsync();
}
