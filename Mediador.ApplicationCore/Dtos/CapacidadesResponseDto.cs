using System.Collections.Generic;

namespace Mediador.ApplicationCore.Dtos;

public class CapacidadesResponseDto
{
    public int IdTransportista { get; set; }
    public string Familia { get; set; } = string.Empty;
    public string VersionConector { get; set; } = string.Empty;
    public string VersionContrato { get; set; } = string.Empty;
    public OperacionesCapacidadDto Operaciones { get; set; } = new();
    public List<string> Eventos { get; set; } = new();
    public bool RecepcionPorBulto { get; set; }
}

public class OperacionesCapacidadDto
{
    public bool PrecioEnLinea { get; set; } = true;
    public bool RegistroEnLinea { get; set; } = true;
    public bool Anulacion { get; set; } = true;
    public bool Puntos { get; set; } = true;
}
