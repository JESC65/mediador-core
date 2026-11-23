using System.Text.Json;

namespace Mediador.ApplicationCore.Dtos;

public class EventoRequestDto
{
    public string IdEvento { get; set; } = string.Empty;
    public string TipoEvento { get; set; } = string.Empty;
    public string? VersionContrato { get; set; }
    public int? Secuencia { get; set; }
    public EntidadEventoDto? Entidad { get; set; }
    public int? VersionEntidad { get; set; }
    public string? Emisor { get; set; }
    public int IdTransportista { get; set; }
    public DateTimeOffset FechaEvento { get; set; }
    public JsonElement Datos { get; set; }
}
