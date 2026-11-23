namespace Mediador.Infrastructure.Models;

public class TransportistaCapacidad
{
    public int IdTransportista { get; set; }
    public string? Familia { get; set; }
    public string? VersionConector { get; set; }
    public string? VersionContrato { get; set; }
    public bool PrecioEnLinea { get; set; } = true;
    public bool RegistroEnLinea { get; set; } = true;
    public bool Anulacion { get; set; } = true;
    public bool Puntos { get; set; } = true;
    public bool RecepcionPorBulto { get; set; }
    public string? EventosJson { get; set; }
    public DateTimeOffset FechaActualizacion { get; set; }
}
