namespace Mediador.ApplicationCore.Dtos.Simulador;

/// <summary>
/// Lo que devuelve el cliente HTTP del simulador: no es la lógica de qué
/// hacer con la respuesta (eso es el viernes, la confirmación) — solo dice
/// si la comunicación funcionó o no, y por qué, para que quien orqueste los
/// reintentos (3.3) tenga con qué decidir.
/// </summary>
public class ResultadoLlamadaSimuladorDto<T>
{
    public bool Exitosa { get; private init; }
    public T? Respuesta { get; private init; }

    /// <summary>"TIMEOUT", "SIN_CONEXION", "ERROR_HTTP_500", "RESPUESTA_VACIA", etc.</summary>
    public string? ErrorComunicacion { get; private init; }

    public static ResultadoLlamadaSimuladorDto<T> Ok(T respuesta) => new() { Exitosa = true, Respuesta = respuesta };
    public static ResultadoLlamadaSimuladorDto<T> Falla(string error) => new() { Exitosa = false, ErrorComunicacion = error };
}
