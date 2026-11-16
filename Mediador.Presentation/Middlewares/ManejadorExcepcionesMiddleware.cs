using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Exceptions;

namespace Mediador.Presentation.Middlewares;

/// <summary>
/// Middleware global para la captura y estandarización de errores HTTP en Mediador (Regla R8).
/// Unifica las respuestas en: { "error": "...", "detalle": "...", "campo": null, "idCorrelacion": "..." }.
/// Protege los detalles técnicos en errores 500 enviándolos exclusivamente al log del servidor.
/// </summary>
public class ManejadorExcepcionesMiddleware
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ManejadorExcepcionesMiddleware> _logger;

    public ManejadorExcepcionesMiddleware(RequestDelegate next, ILogger<ManejadorExcepcionesMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Habilita el buffer de lectura para poder inspeccionar el cuerpo
        // en caso de error no controlado (ej. extraer idCorrelacion).
        context.Request.EnableBuffering();

        try
        {
            await _next(context);
        }
        catch (MediadorApiException ex)
        {
            await ManejarMediadorApiExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            await ManejarExcepcionNoControladaAsync(context, ex);
        }
    }

    private async Task ManejarMediadorApiExceptionAsync(HttpContext context, MediadorApiException ex)
    {
        var idCorrelacion = ex.IdCorrelacion 
            ?? ObtenerIdCorrelacionDeContexto(context) 
            ?? await IntentarLeerIdCorrelacionDelCuerpoAsync(context);

        _logger.LogWarning(
            ex,
            "Error de API Mediador [{StatusCode}] {Error}: {Detalle} | Campo: {Campo} | IdCorrelacion: {IdCorrelacion}",
            ex.StatusCode,
            ex.Error,
            ex.Detalle,
            ex.Campo,
            idCorrelacion);

        var respuesta = new ErrorResponseDto
        {
            Error = ex.Error,
            Detalle = ex.Detalle,
            Campo = ex.Campo,
            IdCorrelacion = idCorrelacion
        };

        await EscribirRespuestaJsonAsync(context, ex.StatusCode, respuesta);
    }

    private async Task ManejarExcepcionNoControladaAsync(HttpContext context, Exception ex)
    {
        var idCorrelacion = ObtenerIdCorrelacionDeContexto(context)
            ?? await IntentarLeerIdCorrelacionDelCuerpoAsync(context)
            ?? context.TraceIdentifier;

        // Registro técnico completo en el log del servidor
        _logger.LogError(
            ex,
            "Excepción no controlada en Mediador (HTTP 500). IdCorrelacion: {IdCorrelacion} | Mensaje: {Mensaje}",
            idCorrelacion,
            ex.Message);

        // Al cliente solo se le expone el código estándar y un mensaje genérico sin detalles técnicos
        var respuesta = new ErrorResponseDto
        {
            Error = "ERROR_INTERNO",
            Detalle = "Ocurrió un error inesperado al procesar la solicitud.",
            Campo = null,
            IdCorrelacion = idCorrelacion
        };

        await EscribirRespuestaJsonAsync(context, StatusCodes.Status500InternalServerError, respuesta);
    }

    private static async Task EscribirRespuestaJsonAsync(HttpContext context, int statusCode, ErrorResponseDto respuesta)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(respuesta, OpcionesJson);
        await context.Response.WriteAsync(json);
    }

    private static string? ObtenerIdCorrelacionDeContexto(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Id-Correlacion", out var h1) && !string.IsNullOrWhiteSpace(h1))
        {
            return h1.ToString();
        }

        if (context.Request.Headers.TryGetValue("idCorrelacion", out var h2) && !string.IsNullOrWhiteSpace(h2))
        {
            return h2.ToString();
        }

        if (context.Request.Headers.TryGetValue("X-Correlation-Id", out var h3) && !string.IsNullOrWhiteSpace(h3))
        {
            return h3.ToString();
        }

        if (context.Request.Query.TryGetValue("idCorrelacion", out var q) && !string.IsNullOrWhiteSpace(q))
        {
            return q.ToString();
        }

        if (context.Items.TryGetValue("idCorrelacion", out var itemVal) && itemVal is string s && !string.IsNullOrWhiteSpace(s))
        {
            return s;
        }

        return null;
    }

    private static async Task<string?> IntentarLeerIdCorrelacionDelCuerpoAsync(HttpContext context)
    {
        try
        {
            if (context.Request.ContentLength > 0 && context.Request.Body.CanSeek)
            {
                context.Request.Body.Position = 0;
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
                var cuerpo = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                if (!string.IsNullOrWhiteSpace(cuerpo))
                {
                    using var doc = JsonDocument.Parse(cuerpo);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                        doc.RootElement.TryGetProperty("idCorrelacion", out var prop) &&
                        prop.ValueKind == JsonValueKind.String)
                    {
                        var valor = prop.GetString();
                        if (!string.IsNullOrWhiteSpace(valor))
                        {
                            return valor;
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignora fallas al parsear el cuerpo para no interrumpir el flujo del error principal
        }

        return null;
    }
}
