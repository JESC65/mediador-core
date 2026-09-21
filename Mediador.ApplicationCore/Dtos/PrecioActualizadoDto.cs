namespace Mediador.ApplicationCore.Dtos;

public class PrecioActualizadoDto
{
    // idRuta acá es el código externo de la ruta (CodigoRutaExterno), no el
    // identificador interno del Mediador — es el mismo formato que B1.
    public string IdRuta { get; set; } = string.Empty;
    public string TipoCarga { get; set; } = string.Empty;
    public decimal PesoDesdeKg { get; set; }
    public decimal PesoHastaKg { get; set; }
    public decimal PrecioAnterior { get; set; }
    public decimal PrecioNuevo { get; set; }
}
