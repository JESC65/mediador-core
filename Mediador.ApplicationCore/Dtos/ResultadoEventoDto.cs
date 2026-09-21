namespace Mediador.ApplicationCore.Dtos;

public class ResultadoEventoDto
{
    public EventoResponseDto? Respuesta { get; private init; }
    public string? EstadoConflicto { get; private init; }

    public static ResultadoEventoDto Ok(EventoResponseDto respuesta) => new() { Respuesta = respuesta };
    public static ResultadoEventoDto Conflicto(string estadoActual) => new() { EstadoConflicto = estadoActual };
}
