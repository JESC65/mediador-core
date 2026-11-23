namespace Mediador.ApplicationCore.Dtos;

public class ResultadoSincronizacionCapacidadesDto
{
    public bool Encontrado { get; set; }
    public bool Exitoso { get; set; }
    public string? Detalle { get; set; }
    public CapacidadesResponseDto? Capacidades { get; set; }

    public static ResultadoSincronizacionCapacidadesDto NoEncontrado(string? detalle = null) =>
        new() { Encontrado = false, Exitoso = false, Detalle = detalle ?? "Transportista no encontrada." };

    public static ResultadoSincronizacionCapacidadesDto Falla(string detalle) =>
        new() { Encontrado = true, Exitoso = false, Detalle = detalle };

    public static ResultadoSincronizacionCapacidadesDto Ok(CapacidadesResponseDto capacidades) =>
        new() { Encontrado = true, Exitoso = true, Capacidades = capacidades };
}
