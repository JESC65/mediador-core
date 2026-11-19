using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Dtos.Simulador;

namespace Mediador.ApplicationCore.Interfaces;

/// <summary>
/// Sabe llamar por HTTP a las operaciones del simulador de una
/// transportista — nada de lógica de negocio todavía (eso es la
/// confirmación completa del viernes, 3.2). urlBaseTransportista es
/// MED.Transportista.UrlConector, leído de la base por quien llame a
/// esta interfaz; el cliente no la fija ni la adivina.
/// </summary>
public interface ISimuladorTransportistaClient
{
    /// <summary>POST {urlBaseTransportista}/cotizar — espera máxima 15 segundos (3.3).</summary>
    Task<ResultadoLlamadaSimuladorDto<SimuladorCotizarResponseDto>> ConsultarPrecio(
        string urlBaseTransportista, SimuladorCotizarRequestDto request);

    /// <summary>POST {urlBaseTransportista}/registrar — espera máxima 30 segundos (3.3).</summary>
    Task<ResultadoLlamadaSimuladorDto<SimuladorRegistrarResponseDto>> Registrar(
        string urlBaseTransportista, SimuladorRegistrarRequestDto request);

    /// <summary>B7: GET {urlBaseTransportista}/v1/eventos?desde={desdeSecuencia} — recuperación de eventos (C2 v2.0 §5).</summary>
    Task<ResultadoLlamadaSimuladorDto<List<EventoRequestDto>>> RecuperarEventos(
        string urlBaseTransportista, int desdeSecuencia);

    /// <summary>B0: GET {urlBaseTransportista}/v1/capacidades — capacidades de la transportista (C2 v2.0 §3).</summary>
    Task<ResultadoLlamadaSimuladorDto<CapacidadesResponseDto>> ObtenerCapacidades(
        string urlBaseTransportista);
}
