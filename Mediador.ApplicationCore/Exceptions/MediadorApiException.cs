namespace Mediador.ApplicationCore.Exceptions;

/// <summary>
/// Excepción de API del Mediador para representar errores HTTP estandarizados (Regla R8).
/// Transporta el código de estado HTTP, el código de error único, el detalle legible,
/// el nombre del campo causante (opcional o null) y el identificador de correlación.
/// </summary>
public class MediadorApiException : Exception
{
    public int StatusCode { get; }
    public string Error { get; }
    public string Detalle { get; }
    public string? Campo { get; }
    public string? IdCorrelacion { get; }

    public MediadorApiException(
        int statusCode,
        string error,
        string detalle,
        string? campo = null,
        string? idCorrelacion = null,
        Exception? innerException = null)
        : base(detalle, innerException)
    {
        StatusCode = statusCode;
        Error = error;
        Detalle = detalle;
        Campo = campo;
        IdCorrelacion = idCorrelacion;
    }
}
