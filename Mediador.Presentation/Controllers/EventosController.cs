using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Exceptions;
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
            throw new MediadorApiException(
                statusCode: StatusCodes.Status409Conflict,
                error: "ESTADO_INVALIDO",
                detalle: $"Transición de estado no permitida. El envío se encuentra en estado '{resultado.EstadoConflicto}'.",
                campo: null,
                idCorrelacion: null);
        }

        return Ok(resultado.Respuesta);
    }
}
