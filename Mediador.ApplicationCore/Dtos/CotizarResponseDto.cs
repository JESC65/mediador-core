using System.Text.Json.Serialization;

namespace Mediador.ApplicationCore.Dtos;

public class CotizarResponseDto
{
    public string IdCorrelacion { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? PesoTotalKg { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CantidadPaquetes { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<OpcionCotizacionDto>? Opciones { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Motivo { get; set; }
}
