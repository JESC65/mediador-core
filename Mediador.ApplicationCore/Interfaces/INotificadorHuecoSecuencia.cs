using System.Threading.Channels;

namespace Mediador.ApplicationCore.Interfaces;

public interface INotificadorHuecoSecuencia
{
    void NotificarHueco(int idTransportista);
    ChannelReader<int> Lector { get; }
}
