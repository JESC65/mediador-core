using System.Net.Http.Json;
using Mediador.ApplicationCore.Dtos.Simulador;
using Mediador.ApplicationCore.Interfaces;
using Microsoft.Extensions.Logging;

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
    private static readonly TimeSpan EsperaConsultarPrecio = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan EsperaRegistrar = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SimuladorTransportistaClient> _logger;

    public SimuladorTransportistaClient(IHttpClientFactory httpClientFactory, ILogger<SimuladorTransportistaClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<ResultadoLlamadaSimuladorDto<SimuladorCotizarResponseDto>> ConsultarPrecio(
        string urlBaseTransportista, SimuladorCotizarRequestDto request) =>
        Llamar<SimuladorCotizarRequestDto, SimuladorCotizarResponseDto>(
            urlBaseTransportista, "cotizar", request, EsperaConsultarPrecio);

    public Task<ResultadoLlamadaSimuladorDto<SimuladorRegistrarResponseDto>> Registrar(
        string urlBaseTransportista, SimuladorRegistrarRequestDto request) =>
        Llamar<SimuladorRegistrarRequestDto, SimuladorRegistrarResponseDto>(
            urlBaseTransportista, "registrar", request, EsperaRegistrar);

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
            using var httpResponse = await cliente.PostAsJsonAsync(url, request, cts.Token);

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
}
