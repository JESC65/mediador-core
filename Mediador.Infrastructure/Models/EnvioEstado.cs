namespace Mediador.Infrastructure.Models;

public class EnvioEstado
{
    public int IdEnvioEstado { get; set; }
    public int IdEnvio { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTimeOffset FechaEstado { get; set; }
    public string Origen { get; set; } = string.Empty;
    public string? Comentario { get; set; }
    public string? Usuario { get; set; }
}
