namespace Mediador.Infrastructure.Models;

public class TransportistaSecuencia
{
    public int IdTransportista { get; set; }
    public int UltimaSecuenciaContigua { get; set; }
    public DateTimeOffset FechaActualizacion { get; set; }
}
