namespace Mediador.ApplicationCore.Dtos.Simulador;

/// <summary>
/// Lo que el Mediador manda a POST /registrar del simulador (paso 3.2 · Paso 5).
/// IMPORTANTE: no debe llevar el precio de venta al cliente final — solo
/// PrecioAcordado, que es el precio real que ya devolvió la transportista.
/// </summary>
public class SimuladorRegistrarRequestDto
{
    public string IdCorrelacion { get; set; } = string.Empty;
    public string CodigoSeguimiento { get; set; } = string.Empty;
    public string IdRuta { get; set; } = string.Empty;
    public decimal PrecioAcordado { get; set; }
    public List<SimuladorPaqueteRegistroDto> Paquetes { get; set; } = new();
    public SimuladorPersonaDto Remitente { get; set; } = new();
    public SimuladorPersonaDto Consignado { get; set; } = new();
}
