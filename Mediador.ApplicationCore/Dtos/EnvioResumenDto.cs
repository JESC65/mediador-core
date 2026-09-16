namespace Mediador.ApplicationCore.Dtos;

public class EnvioResumenDto
{
    public int IdEnvio { get; set; }
    public string Estado { get; set; } = string.Empty;
    public decimal PesoTotalKg { get; set; }
    public int CantidadPaquetes { get; set; }
}
