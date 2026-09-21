namespace Mediador.ApplicationCore.Dtos;

public class EventoParaGuardarDto
{
    public string IdEvento { get; set; } = string.Empty;
    public string TipoEvento { get; set; } = string.Empty;
    public int? IdTransportista { get; set; }
    public int? IdEnvio { get; set; }
    public DateTimeOffset FechaEvento { get; set; }
    public string? Contenido { get; set; }
}
