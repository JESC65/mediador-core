using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Mediador.Presentation.Controllers;

[ApiController]
[Route("eventos")]
public class EventosController : ControllerBase
{
    private readonly IEventoMediadorService _servicio;

    public EventosController(IEventoMediadorService servicio)
    {
        _servicio = servicio;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] EventoRequestDto request)
    {
        var resultado = await _servicio.ProcesarEvento(request);

        if (resultado.EstadoConflicto is not null)
        {
            return Conflict(new
            {
                error = "ESTADO_INVALIDO",
                estadoActual = resultado.EstadoConflicto
            });
        }

        return Ok(resultado.Respuesta);
    }
}
