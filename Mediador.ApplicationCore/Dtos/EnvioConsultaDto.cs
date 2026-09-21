namespace Mediador.ApplicationCore.Dtos;

public class EnvioConsultaDto
{
    public string CodigoSeguimiento { get; set; } = string.Empty;
    public string EstadoActual { get; set; } = string.Empty;
    public int? IdTransportista { get; set; }
    public string? NombreTransportista { get; set; }
    public decimal? PrecioAcordado { get; set; }
    public string? BusAsignado { get; set; }
    public List<HistorialEstadoDto> Historial { get; set; } = new();
}
