using Mediador.ApplicationCore.Dtos;

namespace Mediador.ApplicationCore.Interfaces;

public interface IEnvioRepository
{
    Task<EnvioResumenDto?> BuscarPorIdCorrelacion(string idCorrelacion);
    Task<int> CrearEnvioCotizado(CotizarRequestDto request, decimal pesoTotalKg, int cantidadPaquetes);
    Task<EnvioEventoDto?> BuscarPorCodigoSeguimiento(string codigoSeguimiento);
    Task ActualizarEstado(int idEnvio, string nuevoEstado, string origen, string? comentario);
    Task<EnvioConsultaDto?> ObtenerConsulta(string codigoSeguimiento);
}
