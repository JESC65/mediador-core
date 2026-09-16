namespace Mediador.Infrastructure.Models;

public class TarifaProveedor
{
    public int IdTarifaProveedor { get; set; }
    public int IdRuta { get; set; }
    public string TipoCarga { get; set; } = string.Empty;
    public decimal PesoDesdeKg { get; set; }
    public decimal PesoHastaKg { get; set; }
    public decimal Precio { get; set; }
    public decimal? PrecioMinimo { get; set; }
    public DateOnly VigenteDesde { get; set; }
    public DateOnly? VigenteHasta { get; set; }
    public bool Activo { get; set; }
}
