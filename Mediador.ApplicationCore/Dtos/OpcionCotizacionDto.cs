namespace Mediador.ApplicationCore.Dtos;

public class OpcionCotizacionDto
{
    public string IdOpcion { get; set; } = string.Empty;
    public int IdTransportista { get; set; }
    public string NombreTransportista { get; set; } = string.Empty;
    public decimal PrecioReferencial { get; set; }
    public bool PrecioEsEstimado { get; set; }
    public string? FrecuenciaSalidas { get; set; }
    public bool AceptaFragil { get; set; }
    public DateTimeOffset VigenteHasta { get; set; }
}
