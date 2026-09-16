namespace Mediador.ApplicationCore.Dtos;

public class CotizarRequestDto
{
    public string IdCorrelacion { get; set; } = string.Empty;
    public string IdEnvioCourier { get; set; } = string.Empty;
    public int IdCourier { get; set; }
    public string? UbigeoOrigen { get; set; }
    public string UbigeoDestino { get; set; } = string.Empty;
    public string TipoCarga { get; set; } = string.Empty;
    public DateTimeOffset? FechaSolicitud { get; set; }
    public List<PaqueteCotizarDto> Paquetes { get; set; } = new();
}
