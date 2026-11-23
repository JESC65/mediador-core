using System.Threading.Channels;
using Mediador.ApplicationCore.Interfaces;

namespace Mediador.ApplicationCore.Services;

public class NotificadorHuecoSecuencia : INotificadorHuecoSecuencia
{
    private readonly Channel<int> _canal = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
    {
        SingleReader = true
    });

    public ChannelReader<int> Lector => _canal.Reader;

    public void NotificarHueco(int idTransportista)
    {
        _canal.Writer.TryWrite(idTransportista);
    }
}
