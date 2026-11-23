using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Exceptions;
using Mediador.ApplicationCore.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Mediador.Presentation.Controllers;

[ApiController]
[Route("eventos")]
[Route("v1/eett/eventos")]
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

        if (resultado.EsConflictoContenido)
        {
            throw new MediadorApiException(
                statusCode: StatusCodes.Status409Conflict,
                error: "CONFLICTO_ID_EVENTO",
                detalle: resultado.MensajeConflicto ?? "Ya existe un evento registrado con el mismo idEvento pero con diferente contenido.",
                campo: "idEvento",
                idCorrelacion: null);
        }

        // Regla de oro: HTTP 202 Accepted solo después de haber guardado el evento en base de datos.
        return StatusCode(StatusCodes.Status202Accepted, resultado.Respuesta);
    }
}
