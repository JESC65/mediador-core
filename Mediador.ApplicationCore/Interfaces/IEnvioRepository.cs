using Mediador.ApplicationCore.Dtos;

namespace Mediador.ApplicationCore.Interfaces;

public interface IEnvioRepository
{
    Task<EnvioResumenDto?> BuscarPorIdCorrelacion(string idCorrelacion);
    Task<int> CrearEnvioCotizado(CotizarRequestDto request, decimal pesoTotalKg, int cantidadPaquetes);
}
