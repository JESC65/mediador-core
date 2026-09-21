using System.Text.Json;

namespace Mediador.ApplicationCore.Dtos;

public class EventoRequestDto
{
    public string IdEvento { get; set; } = string.Empty;
    public string TipoEvento { get; set; } = string.Empty;
    public int IdTransportista { get; set; }
    public DateTimeOffset FechaEvento { get; set; }
    public JsonElement Datos { get; set; }
}
