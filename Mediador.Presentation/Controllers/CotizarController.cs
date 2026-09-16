using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Mediador.Presentation.Controllers;

[ApiController]
[Route("cotizar")]
public class CotizarController : ControllerBase
{
    private readonly ICotizacionMediadorService _servicio;

    public CotizarController(ICotizacionMediadorService servicio)
    {
        _servicio = servicio;
    }


    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CotizarRequestDto request)
    {
        var resultado = await _servicio.Cotizar(request);

        if (resultado.EstadoConflicto is not null)
        {
            return Conflict(new
            {
                idCorrelacion = request.IdCorrelacion,
                estadoActual = resultado.EstadoConflicto,
                mensaje = "Ya existe un envío con este idCorrelacion en un estado distinto de COTIZADO."
            });
        }

        return Ok(resultado.Respuesta);
    }
}
