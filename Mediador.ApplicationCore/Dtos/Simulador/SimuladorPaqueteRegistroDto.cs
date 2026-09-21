namespace Mediador.ApplicationCore.Dtos.Simulador;

public class SimuladorPaqueteRegistroDto
{
    public string Descripcion { get; set; } = string.Empty;
    public decimal PesoKg { get; set; }
    public bool EsFragil { get; set; }
    public decimal ValorDeclarado { get; set; }
}
