using Mediador.ApplicationCore.Dtos;
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
            return NotFound(new { error = "OPCION_NO_ENCONTRADA", idOpcion = request.IdOpcion });
        }

        return Ok(resultado);
    }
}
