namespace Mediador.Infrastructure.Models;

public class SecuenciaHueco
{
    public int IdHueco { get; set; }
    public int IdTransportista { get; set; }
    public int SecuenciaEsperada { get; set; }
    public int SecuenciaRecibida { get; set; }
    public DateTimeOffset FechaDeteccion { get; set; }
    public bool Resuelto { get; set; }
    public DateTimeOffset? FechaResolucion { get; set; }
}
