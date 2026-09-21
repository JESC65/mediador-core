namespace Mediador.ApplicationCore.Dtos;

public class PaqueteConfirmarDto
{
    public string? Descripcion { get; set; }
    public decimal PesoKg { get; set; }
    public decimal? LargoCm { get; set; }
    public decimal? AnchoCm { get; set; }
    public decimal? AltoCm { get; set; }
    public bool EsFragil { get; set; }
    public decimal? ValorDeclarado { get; set; }
}
