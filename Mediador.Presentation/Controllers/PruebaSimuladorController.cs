using Mediador.ApplicationCore.Dtos.Simulador;
using Mediador.ApplicationCore.Interfaces;
using Mediador.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mediador.Presentation.Controllers;

/// <summary>
/// Endpoint de prueba, no de negocio: sirve para probar hoy que el
/// cliente del simulador funciona de punta a punta (la captura que pide
/// la Tarea 4 de hoy), antes de que exista la confirmación real que lo
/// use de verdad el viernes. Por eso consulta el DbContext directo en vez
/// de pasar por un repositorio — es un atajo a propósito para esta
/// prueba, no el patrón a seguir en el resto del proyecto. Se puede
/// borrar cuando 3.2 esté construida y lo reemplace.
/// </summary>
[ApiController]
[Route("prueba/simulador")]
public class PruebaSimuladorController : ControllerBase
{
    private readonly MediadorDbContext _db;
    private readonly ISimuladorTransportistaClient _cliente;

    public PruebaSimuladorController(MediadorDbContext db, ISimuladorTransportistaClient cliente)
    {
        _db = db;
        _cliente = cliente;
    }

    /// <summary>
    /// POST /prueba/simulador/{idTransportista}/cotizar
    /// Lee UrlConector de MED.Transportista y llama al POST /cotizar real del simulador.
    /// </summary>
    [HttpPost("{idTransportista:int}/cotizar")]
    public async Task<IActionResult> ConsultarPrecio(int idTransportista, [FromBody] SimuladorCotizarRequestDto request)
    {
        var urlConector = await _db.Transportistas.AsNoTracking()
            .Where(t => t.IdTransportista == idTransportista)
            .Select(t => t.UrlConector)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(urlConector))
        {
            return NotFound($"La transportista {idTransportista} no existe o no tiene UrlConector configurada.");
        }

        var resultado = await _cliente.ConsultarPrecio(urlConector, request);

        if (!resultado.Exitosa)
        {
            return StatusCode(502, new { error = resultado.ErrorComunicacion, urlConector });
        }

        return Ok(resultado.Respuesta);
    }

    /// <summary>
    /// POST /prueba/simulador/{idTransportista}/registrar
    /// Lee UrlConector de MED.Transportista y llama al POST /registrar real del simulador.
    /// </summary>
    [HttpPost("{idTransportista:int}/registrar")]
    public async Task<IActionResult> Registrar(int idTransportista, [FromBody] SimuladorRegistrarRequestDto request)
    {
        var urlConector = await _db.Transportistas.AsNoTracking()
            .Where(t => t.IdTransportista == idTransportista)
            .Select(t => t.UrlConector)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(urlConector))
        {
            return NotFound($"La transportista {idTransportista} no existe o no tiene UrlConector configurada.");
        }

        var resultado = await _cliente.Registrar(urlConector, request);

        if (!resultado.Exitosa)
        {
            return StatusCode(502, new { error = resultado.ErrorComunicacion, urlConector });
        }

        return Ok(resultado.Respuesta);
    }
}
