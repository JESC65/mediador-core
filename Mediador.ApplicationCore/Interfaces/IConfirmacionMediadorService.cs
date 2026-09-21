using Mediador.ApplicationCore.Dtos;

namespace Mediador.ApplicationCore.Interfaces;

public interface IConfirmacionMediadorService
{
    /// <returns>null si el idOpcion no existe (el controlador responde 404).</returns>
    Task<ConfirmarResponseDto?> Confirmar(ConfirmarRequestDto request);
}
