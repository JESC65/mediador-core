using Mediador.ApplicationCore.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Mediador.Presentation.Controllers;

[ApiController]
[Route("envio")]
public class EnvioController : ControllerBase
{
    private readonly IEnvioConsultaService _servicio;

    public EnvioController(IEnvioConsultaService servicio)
    {
        _servicio = servicio;
    }

    [HttpGet("{codigoSeguimiento}")]
    public async Task<IActionResult> Get(string codigoSeguimiento)
    {
        var resultado = await _servicio.ConsultarEstado(codigoSeguimiento);

        return resultado is null ? NotFound() : Ok(resultado);
    }
}
