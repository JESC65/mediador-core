namespace Mediador.ApplicationCore.Dtos.Simulador;

/// <summary>Espejo de CotizarResponseDto del simulador — los dos casos (acepta/rechaza) en una sola forma.</summary>
public class SimuladorCotizarResponseDto
{
    public string IdCorrelacion { get; set; } = string.Empty;
    public bool Disponible { get; set; }
    public decimal? Precio { get; set; }
    public string? DetalleCalculo { get; set; }
    public DateTimeOffset? VigenteHasta { get; set; }
    public string? Motivo { get; set; }
}
