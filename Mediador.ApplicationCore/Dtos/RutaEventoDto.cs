namespace Mediador.ApplicationCore.Dtos;

public class RutaEventoDto
{
    // Código externo de la ruta (ej. "R-LIM-CHI"), no el identificador interno.
    public string IdRuta { get; set; } = string.Empty;
    public string UbigeoOrigen { get; set; } = string.Empty;
    public string UbigeoDestino { get; set; } = string.Empty;
    public string? NombreOrigen { get; set; }
    public string? NombreDestino { get; set; }
    public string? FrecuenciaSalidas { get; set; }
    public string? HoraSalidaHabitual { get; set; }
    public bool AceptaFragil { get; set; }
    public decimal? PesoMaximoKg { get; set; }
    public bool Activa { get; set; } = true;
}
