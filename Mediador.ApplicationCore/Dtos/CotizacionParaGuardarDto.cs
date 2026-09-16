namespace Mediador.ApplicationCore.Dtos;

public class CotizacionParaGuardarDto
{
    public int IdEnvio { get; set; }
    public int IdTransportista { get; set; }
    public string NombreTransportista { get; set; } = string.Empty;
    public int? IdTarifaProveedor { get; set; }
    public string IdOpcion { get; set; } = string.Empty;
    public decimal? PrecioProveedor { get; set; }
    public bool PrecioEsEstimado { get; set; } = true;
    public string? FrecuenciaSalidas { get; set; }
    public bool AceptaFragil { get; set; }
    public bool Descartada { get; set; }
    public string? MotivoDescarte { get; set; }
    public DateTimeOffset VigenteHasta { get; set; }
}
