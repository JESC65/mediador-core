namespace Mediador.Infrastructure.Models;

public class PuntoRecepcion
{
    public int IdPunto { get; set; }
    public int IdTransportista { get; set; }
    public string CodigoExterno { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string Ubigeo { get; set; } = string.Empty;
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public string? Horario { get; set; }
    public int? TiempoServicioMinutos { get; set; }
    public int Version { get; set; } = 1;
    public bool Activo { get; set; } = true;
    public DateTimeOffset FechaActualizacion { get; set; }
}
