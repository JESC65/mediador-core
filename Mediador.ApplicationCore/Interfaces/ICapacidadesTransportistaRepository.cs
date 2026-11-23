using Mediador.ApplicationCore.Dtos;

namespace Mediador.ApplicationCore.Interfaces;

public class TransportistaInfoCapacidadesDto
{
    public int IdTransportista { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public string? UrlConector { get; set; }
    public bool Activo { get; set; }
}

public interface ICapacidadesTransportistaRepository
{
    Task<TransportistaInfoCapacidadesDto?> ObtenerTransportistaAsync(int idTransportista);
    Task<IReadOnlyList<TransportistaInfoCapacidadesDto>> ObtenerActivasConUrlAsync();
    Task GuardarCapacidadesAsync(int idTransportista, CapacidadesResponseDto capacidades);
}
