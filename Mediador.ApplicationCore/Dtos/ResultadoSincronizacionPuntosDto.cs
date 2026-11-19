namespace Mediador.ApplicationCore.Dtos;

public class ResultadoSincronizacionPuntosDto
{
    public bool Encontrado { get; set; }
    public bool Soportado { get; set; }
    public bool Exitoso { get; set; }
    public string? Detalle { get; set; }
    public int PuntosSincronizados { get; set; }

    public static ResultadoSincronizacionPuntosDto NoEncontrado(string? detalle = null) =>
        new() { Encontrado = false, Soportado = false, Exitoso = false, Detalle = detalle ?? "Transportista no encontrada." };

    public static ResultadoSincronizacionPuntosDto NoSoportado(string? detalle = null) =>
        new() { Encontrado = true, Soportado = false, Exitoso = true, Detalle = detalle ?? "La transportista no soporta la consulta de puntos de recepción." };

    public static ResultadoSincronizacionPuntosDto Falla(string detalle) =>
        new() { Encontrado = true, Soportado = true, Exitoso = false, Detalle = detalle };

    public static ResultadoSincronizacionPuntosDto Ok(int puntosSincronizados) =>
        new() { Encontrado = true, Soportado = true, Exitoso = true, PuntosSincronizados = puntosSincronizados };
}
