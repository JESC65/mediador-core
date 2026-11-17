using Mediador.ApplicationCore.Dtos;

namespace Mediador.ApplicationCore.Interfaces;

public interface IEventoRepository
{
    Task<bool> ExisteEvento(string idEvento);
    Task<EventoExistenteDto?> ObtenerEventoExistente(string idEvento);
    Task<int> GuardarEvento(EventoParaGuardarDto evento);
    Task MarcarComoProcesado(int idEventoRecibido);

    Task<int> ObtenerUltimaSecuenciaContigua(int idTransportista);
    Task ActualizarUltimaSecuenciaContigua(int idTransportista, int secuencia);
    Task RegistrarHueco(int idTransportista, int secuenciaEsperada, int secuenciaRecibida);
    Task<List<int>> ObtenerSecuenciasGuardadasMayoresA(int idTransportista, int secuencia);
    Task ResolverHuecosHasta(int idTransportista, int secuenciaContigua);
}
