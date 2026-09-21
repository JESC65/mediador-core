namespace Mediador.Infrastructure.Models;

public class EnvioPersona
{
    public int IdEnvioPersona { get; set; }
    public int IdEnvio { get; set; }
    public string Rol { get; set; } = string.Empty; // REMITENTE | CONSIGNADO
    public string? TipoDocumento { get; set; }
    public string? NumeroDocumento { get; set; }
    public string? Nombres { get; set; }
    public string? Apellidos { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public string? Ubigeo { get; set; }
}
