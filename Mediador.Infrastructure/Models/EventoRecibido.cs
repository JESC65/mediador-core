namespace Mediador.Infrastructure.Models;

public class EventoRecibido
{
    public int IdEventoRecibido { get; set; }
    public string IdEvento { get; set; } = string.Empty;
    public string TipoEvento { get; set; } = string.Empty;
    public int? IdTransportista { get; set; }
    public int? IdEnvio { get; set; }
    public DateTimeOffset FechaEvento { get; set; }
    public DateTimeOffset FechaRecepcion { get; set; }
    public bool Procesado { get; set; }
    public string? Contenido { get; set; }
}
