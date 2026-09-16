namespace Mediador.Infrastructure.Models;

public class Envio
{
    public int IdEnvio { get; set; }
    public string? CodigoSeguimiento { get; set; }
    public string IdCorrelacion { get; set; } = string.Empty;
    public string IdEnvioCourier { get; set; } = string.Empty;
    public int IdCourier { get; set; }
    public string? UbigeoOrigen { get; set; }
    public string UbigeoDestino { get; set; } = string.Empty;
    public string? TipoCarga { get; set; }
    public decimal PesoTotalKg { get; set; }
    public int CantidadPaquetes { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int? IdTransportistaElegida { get; set; }
    public int? IdCotizacionElegida { get; set; }
    public DateTimeOffset FechaRegistro { get; set; }
    public DateTimeOffset? FechaConfirmacion { get; set; }
    public string? Observacion { get; set; }
}
