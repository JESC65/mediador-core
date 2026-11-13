using System.Net.Http.Json;
using Mediador.ApplicationCore.Dtos.Simulador;
using Mediador.ApplicationCore.Interfaces;
using Mediador.ApplicationCore.Utilidades;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mediador.Infrastructure.Clientes;

/// <summary>
/// La implementación real: arma el POST hacia el simulador y trae la
/// respuesta. Sin reintentos todavía — eso es 3.3, y pertenece a la
/// confirmación (viernes), que es quien decide cuándo reintentar y cuándo
/// simplemente descartar la opción. Hoy esto solo intenta una vez, con el
/// tiempo de espera explícito que pide cada operación, y reporta si la
/// comunicación funcionó o no.
/// </summary>
public class SimuladorTransportistaClient : ISimuladorTransportistaClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SimuladorTransportistaClient> _logger;
    private readonly ParametrosOptions _parametros;
    private readonly IConfiguration _configuration;

    public SimuladorTransportistaClient(
        IHttpClientFactory httpClientFactory,
        ILogger<SimuladorTransportistaClient> logger,
        IOptions<ParametrosOptions> parametros,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _parametros = parametros.Value;
        _configuration = configuration;
    }

    public Task<ResultadoLlamadaSimuladorDto<SimuladorCotizarResponseDto>> ConsultarPrecio(
        string urlBaseTransportista, SimuladorCotizarRequestDto request) =>
        Llamar<SimuladorCotizarRequestDto, SimuladorCotizarResponseDto>(
            urlBaseTransportista, "cotizar", request, _parametros.t_precio_eett);

    public Task<ResultadoLlamadaSimuladorDto<SimuladorRegistrarResponseDto>> Registrar(
        string urlBaseTransportista, SimuladorRegistrarRequestDto request) =>
        Llamar<SimuladorRegistrarRequestDto, SimuladorRegistrarResponseDto>(
            urlBaseTransportista, "registrar", request, _parametros.t_registro_eett);

    private async Task<ResultadoLlamadaSimuladorDto<TRespuesta>> Llamar<TPeticion, TRespuesta>(
        string urlBaseTransportista, string ruta, TPeticion request, TimeSpan esperaMaxima)
    {
        var url = $"{urlBaseTransportista.TrimEnd('/')}/{ruta}";
        var cliente = _httpClientFactory.CreateClient();

        // Tiempo de espera explícito por llamada (no el timeout global del
        // HttpClient): así /cotizar y /registrar pueden tener cada uno el
        // suyo (15s y 30s) sin pisarse entre sí.
        using var cts = new CancellationTokenSource(esperaMaxima);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(request)
            };

            var escenario = ObtenerEscenarioConfigurado(url, ruta);
            if (!string.IsNullOrWhiteSpace(escenario))
            {
                httpRequest.Headers.TryAddWithoutValidation("X-Escenario", escenario);
            }

            using var httpResponse = await cliente.SendAsync(httpRequest, cts.Token);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("El simulador respondió {StatusCode} en {Url}", (int)httpResponse.StatusCode, url);
                return ResultadoLlamadaSimuladorDto<TRespuesta>.Falla($"ERROR_HTTP_{(int)httpResponse.StatusCode}");
            }

            var cuerpo = await httpResponse.Content.ReadFromJsonAsync<TRespuesta>(cancellationToken: cts.Token);
            if (cuerpo is null)
            {
                _logger.LogWarning("El simulador respondió 2xx pero sin cuerpo interpretable en {Url}", url);
                return ResultadoLlamadaSimuladorDto<TRespuesta>.Falla("RESPUESTA_VACIA");
            }

            return ResultadoLlamadaSimuladorDto<TRespuesta>.Ok(cuerpo);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Se agotó la espera de {Segundos}s llamando a {Url}", esperaMaxima.TotalSeconds, url);
            return ResultadoLlamadaSimuladorDto<TRespuesta>.Falla("TIMEOUT");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "No se pudo conectar a {Url}", url);
            return ResultadoLlamadaSimuladorDto<TRespuesta>.Falla("SIN_CONEXION");
        }
    }

    private string? ObtenerEscenarioConfigurado(string url, string ruta)
    {
        // 1. Variable de entorno directa (ej. para pruebas específicas sin modificar archivos)
        if (ruta.Equals("registrar", StringComparison.OrdinalIgnoreCase))
        {
            var envReg = Environment.GetEnvironmentVariable("SIMULADOR_ESCENARIO_REGISTRAR");
            if (!string.IsNullOrWhiteSpace(envReg)) return envReg;
        }

        // 2. Mapeo en IConfiguration (appsettings.json / appsettings.Development.json)
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            // Especificidad alta: puerto/ruta o autoridad/ruta (ej. "7201/registrar")
            var porPuertoRuta = _configuration[$"Simulador:Escenarios:{uri.Port}/{ruta}"];
            if (!string.IsNullOrWhiteSpace(porPuertoRuta)) return porPuertoRuta;

            var porHostRuta = _configuration[$"Simulador:Escenarios:{uri.Authority}/{ruta}"];
            if (!string.IsNullOrWhiteSpace(porHostRuta)) return porHostRuta;

            // Por ruta / operación ("registrar" o "cotizar")
            var porRuta = _configuration[$"Simulador:Escenarios:{ruta}"];
            if (!string.IsNullOrWhiteSpace(porRuta)) return porRuta;

            // Por puerto o autoridad general (afecta tanto a cotizar como a registrar para ese puerto)
            var porPuerto = _configuration[$"Simulador:Escenarios:{uri.Port}"];
            if (!string.IsNullOrWhiteSpace(porPuerto)) return porPuerto;

            var porHost = _configuration[$"Simulador:Escenarios:{uri.Authority}"];
            if (!string.IsNullOrWhiteSpace(porHost)) return porHost;
        }

        // Escenario global
        var general = _configuration["Simulador:Escenarios:General"] ?? _configuration["Simulador:Escenario"];
        return !string.IsNullOrWhiteSpace(general) ? general : null;
    }
}
