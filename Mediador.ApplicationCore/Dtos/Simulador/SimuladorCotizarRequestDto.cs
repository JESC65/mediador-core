namespace Mediador.ApplicationCore.Dtos.Simulador;

/// <summary>Lo que el Mediador manda a POST /cotizar del simulador (paso 3.2 · Paso 3).</summary>
public class SimuladorCotizarRequestDto
{
    public string IdCorrelacion { get; set; } = string.Empty;
    public string IdRuta { get; set; } = string.Empty;
    public string TipoCarga { get; set; } = string.Empty;
    public List<SimuladorPaqueteCotizarDto> Paquetes { get; set; } = new();
}
