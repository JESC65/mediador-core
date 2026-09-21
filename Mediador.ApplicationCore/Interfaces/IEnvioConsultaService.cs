using Mediador.ApplicationCore.Dtos;

namespace Mediador.ApplicationCore.Interfaces;

public interface IEnvioConsultaService
{
    Task<EnvioConsultaDto?> ConsultarEstado(string codigoSeguimiento);
}
