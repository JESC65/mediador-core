using Mediador.ApplicationCore.Dtos;

namespace Mediador.ApplicationCore.Interfaces;

public interface IEventoMediadorService
{
    Task<ResultadoEventoDto> ProcesarEvento(EventoRequestDto request);
}
