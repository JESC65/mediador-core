using Mediador.ApplicationCore.Dtos;

namespace Mediador.ApplicationCore.Interfaces;

public interface ICotizacionMediadorService
{
    Task<ResultadoCotizacionDto> Cotizar(CotizarRequestDto request);
}
