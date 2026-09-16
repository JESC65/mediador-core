namespace Mediador.ApplicationCore.Dtos;
public class TransportistaCandidataDto
{
    public int IdTransportista { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public int IdRuta { get; set; }
    public bool AceptaFragil { get; set; }
    public decimal? PesoMaximoKg { get; set; }
    public string? FrecuenciaSalidas { get; set; }
}
