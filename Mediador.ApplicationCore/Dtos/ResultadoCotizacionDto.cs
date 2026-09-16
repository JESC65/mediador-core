namespace Mediador.ApplicationCore.Dtos;

public class ResultadoCotizacionDto
{
    public CotizarResponseDto? Respuesta { get; private init; }
    public string? EstadoConflicto { get; private init; }

    public static ResultadoCotizacionDto Ok(CotizarResponseDto respuesta) => new() { Respuesta = respuesta };
    public static ResultadoCotizacionDto Conflicto(string estadoActual) => new() { EstadoConflicto = estadoActual };
}
