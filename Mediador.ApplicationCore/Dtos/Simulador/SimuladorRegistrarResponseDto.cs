namespace Mediador.ApplicationCore.Dtos.Simulador;

public class SimuladorRegistrarResponseDto
{
    public string IdCorrelacion { get; set; } = string.Empty;
    public bool Aceptado { get; set; }
    public string? NumeroGuiaEETT { get; set; }
    public DateTimeOffset? FechaRegistro { get; set; }
    public string? Motivo { get; set; }
}
