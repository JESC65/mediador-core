namespace Mediador.ApplicationCore.Dtos;

public class CotizacionParaConfirmarDto
{
    public int IdCotizacion { get; set; }
    public string IdOpcion { get; set; } = string.Empty;
    public int IdEnvio { get; set; }
    public int? IdTarifaProveedor { get; set; }
    public decimal? PrecioProveedor { get; set; }
    public DateTimeOffset VigenteHasta { get; set; }

    public int IdTransportista { get; set; }
    public string NombreTransportista { get; set; } = string.Empty;
    public string? UrlConector { get; set; }

    // Código externo de la ruta (Ruta.CodigoRutaExterno) — lo que espera B3/B4, no el id interno.
    public string CodigoRutaExterno { get; set; } = string.Empty;

    public string TipoCarga { get; set; } = string.Empty;

    // Estado actual del envío al momento de buscar la opción — para el paso 1 (idempotencia).
    public string? CodigoSeguimientoEnvio { get; set; }
}
