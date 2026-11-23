using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Exceptions;
using Mediador.ApplicationCore.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Mediador.Presentation.Controllers;

[ApiController]
[Route("confirmar")]
public class ConfirmarController : ControllerBase
{
    private readonly IConfirmacionMediadorService _servicio;

    public ConfirmarController(IConfirmacionMediadorService servicio)
    {
        _servicio = servicio;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ConfirmarRequestDto request)
    {
        var resultado = await _servicio.Confirmar(request);

        if (resultado is null)
        {
            throw new MediadorApiException(
                statusCode: StatusCodes.Status404NotFound,
                error: "OPCION_NO_ENCONTRADA",
                detalle: $"No se encontró la opción de cotización '{request.IdOpcion}'.",
                campo: "idOpcion",
                idCorrelacion: request.IdCorrelacion);
        }

        return Ok(resultado);
    }
}
