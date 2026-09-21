namespace Mediador.ApplicationCore.Dtos;

public class HistorialEstadoDto
{
    public string Estado { get; set; } = string.Empty;
    public DateTimeOffset Fecha { get; set; }
    public string Origen { get; set; } = string.Empty;
    public string? Comentario { get; set; }
}
