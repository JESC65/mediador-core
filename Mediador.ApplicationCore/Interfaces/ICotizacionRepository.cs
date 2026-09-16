using Mediador.ApplicationCore.Dtos;

namespace Mediador.ApplicationCore.Interfaces;

public interface ICotizacionRepository
{
    Task GuardarCotizaciones(IReadOnlyList<CotizacionParaGuardarDto> cotizaciones);
    Task<IReadOnlyList<OpcionCotizacionDto>> ObtenerNoDescartadas(int idEnvio);
}
