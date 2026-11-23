namespace Mediador.ApplicationCore.Dtos;

/// <summary>
/// Modelo de respuesta uniforme para errores HTTP del Mediador según la Regla R8 de los Contratos C1 y C2.
/// Si un campo no aplica, debe serializarse explícitamente en null.
/// </summary>
public class ErrorResponseDto
{
    public string Error { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    public string? Campo { get; set; }
    public string? IdCorrelacion { get; set; }
}
