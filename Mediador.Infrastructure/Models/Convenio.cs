namespace Mediador.Infrastructure.Models;

public class Convenio
{
    public int IdConvenio { get; set; }
    public int IdCourier { get; set; }
    public int IdTransportista { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public string? Observacion { get; set; }
    public bool Activo { get; set; }
}
