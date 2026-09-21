using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Interfaces;

namespace Mediador.ApplicationCore.Services;

public class EnvioConsultaService : IEnvioConsultaService
{
    private readonly IEnvioRepository _envioRepositorio;

    public EnvioConsultaService(IEnvioRepository envioRepositorio)
    {
        _envioRepositorio = envioRepositorio;
    }

    public Task<EnvioConsultaDto?> ConsultarEstado(string codigoSeguimiento) =>
        _envioRepositorio.ObtenerConsulta(codigoSeguimiento);
}
