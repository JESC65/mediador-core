namespace Mediador.Infrastructure.Models;

public class Ruta
{
    public int IdRuta { get; set; }
    public int IdTransportista { get; set; }
    public string CodigoRutaExterno { get; set; } = string.Empty;
    public string UbigeoOrigen { get; set; } = string.Empty;
    public string UbigeoDestino { get; set; } = string.Empty;
    public string? NombreOrigen { get; set; }
    public string? NombreDestino { get; set; }
    public string? FrecuenciaSalidas { get; set; }
    public string? HoraSalidaHabitual { get; set; }
    public bool AceptaFragil { get; set; }
    public decimal? PesoMaximoKg { get; set; }
    public bool Activa { get; set; }
    public DateTimeOffset FechaActualizacion { get; set; }
}
