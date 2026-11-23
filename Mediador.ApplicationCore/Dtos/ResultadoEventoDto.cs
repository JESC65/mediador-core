namespace Mediador.ApplicationCore.Dtos;

public class ResultadoEventoDto
{
    public EventoResponseDto? Respuesta { get; private init; }
    public bool EsConflictoContenido { get; private init; }
    public string? MensajeConflicto { get; private init; }

    public static ResultadoEventoDto Ok(EventoResponseDto respuesta) => new() { Respuesta = respuesta };
    public static ResultadoEventoDto ConflictoContenido(string mensaje) => new()
    {
        EsConflictoContenido = true,
        MensajeConflicto = mensaje
    };
}
