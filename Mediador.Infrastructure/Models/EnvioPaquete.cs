namespace Mediador.Infrastructure.Models;

public class EnvioPaquete
{
    public int IdEnvioPaquete { get; set; }
    public int IdEnvio { get; set; }
    public string? Descripcion { get; set; }
    public decimal PesoKg { get; set; }
    public int Cantidad { get; set; } = 1;
    public decimal? LargoCm { get; set; }
    public decimal? AnchoCm { get; set; }
    public decimal? AltoCm { get; set; }
    public bool? EsFragil { get; set; }
    public decimal? ValorDeclarado { get; set; }
}
