namespace Mediador.ApplicationCore.Dtos;

public class PaqueteCotizarDto
{
    public string? Descripcion { get; set; }
    public decimal PesoKg { get; set; }
    public int Cantidad { get; set; } = 1;
    public bool EsFragil { get; set; }
}
