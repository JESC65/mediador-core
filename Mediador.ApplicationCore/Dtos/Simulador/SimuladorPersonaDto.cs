namespace Mediador.ApplicationCore.Dtos.Simulador;

/// <summary>Espejo de PersonaRegistroDto del simulador. Direccion/Ubigeo solo van en consignado.</summary>
public class SimuladorPersonaDto
{
    public string TipoDocumento { get; set; } = string.Empty;
    public string NumeroDocumento { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Ubigeo { get; set; }
}
