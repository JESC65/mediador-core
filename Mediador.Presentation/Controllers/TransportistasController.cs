using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Mediador.Presentation.Controllers;

[ApiController]
[Route("v1/transportistas")]
public class TransportistasController : ControllerBase
{
    private readonly ICapacidadesTransportistaService _capacidadesService;

    public TransportistasController(ICapacidadesTransportistaService capacidadesService)
    {
        _capacidadesService = capacidadesService;
    }

    /// <summary>
    /// POST /v1/transportistas/{id}/capacidades — Dispara la sincronización B0 de capacidades.
    /// Devuelve 404 RECURSO_NO_ENCONTRADO si la transportista no existe (R8).
    /// </summary>
    [HttpPost("{id:int}/capacidades")]
    public async Task<IActionResult> SincronizarCapacidades([FromRoute] int id)
    {
        var resultado = await _capacidadesService.SincronizarTransportistaAsync(id);

        if (!resultado.Encontrado)
        {
            return NotFound(new ErrorResponseDto
            {
                Error = "RECURSO_NO_ENCONTRADO",
                Detalle = $"La transportista con Id {id} no existe.",
                Campo = null,
                IdCorrelacion = null
            });
        }

        if (!resultado.Exitoso)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponseDto
            {
                Error = "ERROR_CONECTOR",
                Detalle = resultado.Detalle ?? "No se pudieron obtener las capacidades de la transportista.",
                Campo = null,
                IdCorrelacion = null
            });
        }

        return Ok(resultado.Capacidades);
    }
}
