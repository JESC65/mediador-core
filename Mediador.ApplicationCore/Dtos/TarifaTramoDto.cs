namespace Mediador.ApplicationCore.Dtos;

public class TarifaTramoDto
{
    public int IdTarifaProveedor { get; set; }
    public decimal Precio { get; set; }
    public decimal? PrecioMinimo { get; set; }
}
