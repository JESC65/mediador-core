namespace Mediador.ApplicationCore.Dtos;

public class ConfirmarRequestDto
{
    public string IdCorrelacion { get; set; } = string.Empty;
    public string IdOpcion { get; set; } = string.Empty;
    public PersonaConfirmarDto Remitente { get; set; } = new();
    public PersonaConfirmarDto Consignado { get; set; } = new();
}
