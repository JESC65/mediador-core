namespace Mediador.Infrastructure.Models;

public class Cotizacion
{
    public int IdCotizacion { get; set; }
    public int IdEnvio { get; set; }
    public int IdTransportista { get; set; }
    public int? IdTarifaProveedor { get; set; }
    public string IdOpcion { get; set; } = string.Empty;
    public decimal? PrecioProveedor { get; set; }
    public bool PrecioEsEstimado { get; set; } = true;
    public string? FrecuenciaSalidas { get; set; }
    public bool Elegida { get; set; }
    public bool Descartada { get; set; }
    public string? MotivoDescarte { get; set; }
    public DateTimeOffset FechaCotizacion { get; set; }
    public DateTimeOffset VigenteHasta { get; set; }
}
