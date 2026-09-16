namespace Mediador.Infrastructure.Models;

public class Transportista
{
    public int IdTransportista { get; set; }
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string? NombreComercial { get; set; }
    public string? UrlConector { get; set; }
    public string? Telefono { get; set; }
    public bool Activo { get; set; }
    public DateTimeOffset FechaRegistro { get; set; }
}
