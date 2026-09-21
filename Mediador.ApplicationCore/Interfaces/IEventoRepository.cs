using Mediador.ApplicationCore.Dtos;

namespace Mediador.ApplicationCore.Interfaces;

public interface IEventoRepository
{
    Task<bool> ExisteEvento(string idEvento);
    Task<int> GuardarEvento(EventoParaGuardarDto evento);
    Task MarcarComoProcesado(int idEventoRecibido);
}
