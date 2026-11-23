using System.Collections.Generic;

namespace Mediador.ApplicationCore.Dtos;

public class PuntosTransportistaResponseDto
{
    public int IdTransportista { get; set; }
    public List<PuntoRecepcionDto> Puntos { get; set; } = new();
}

public class PuntoRecepcionDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string Ubigeo { get; set; } = string.Empty;
}
