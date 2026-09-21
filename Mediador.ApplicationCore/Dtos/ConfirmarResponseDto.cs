using System.Text.Json.Serialization;

namespace Mediador.ApplicationCore.Dtos;

public class ConfirmarResponseDto
{
    public string IdCorrelacion { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;

    // CONFIRMADO / YA_CONFIRMADO
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CodigoSeguimiento { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? IdTransportista { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NombreTransportista { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? PrecioFinal { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PrecioCambio { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? FechaConfirmacion { get; set; }

    // PRECIO_CAMBIO
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? PrecioReferencial { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? PrecioReal { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RequiereConfirmacion { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdOpcionActualizada { get; set; }

    // Motivo de rechazo, cuando aplica (OPCION_EXPIRADA, OPCION_NO_DISPONIBLE, etc.)
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Motivo { get; set; }

    // Solo aparece si el paso 5 (registrar) tuvo éxito pero el 7 (transacción) falló:
    // el envío ya existe del lado de la transportista y no debe perderse ese dato.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NumeroGuiaEett { get; set; }
}
