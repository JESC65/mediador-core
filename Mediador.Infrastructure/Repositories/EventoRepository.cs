using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Interfaces;
using Mediador.ApplicationCore.Utilidades;
using Mediador.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Mediador.Infrastructure.Repositories;

public class EventoRepository : IEventoRepository
{
    private readonly MediadorDbContext _db;

    public EventoRepository(MediadorDbContext db)
    {
        _db = db;
    }

    public async Task<bool> ExisteEvento(string idEvento) =>
        await _db.EventosRecibidos.AsNoTracking().AnyAsync(e => e.IdEvento == idEvento);

    public async Task<int> GuardarEvento(EventoParaGuardarDto evento)
    {
        var entidad = new Models.EventoRecibido
        {
            IdEvento = evento.IdEvento,
            TipoEvento = evento.TipoEvento,
            IdTransportista = evento.IdTransportista,
            IdEnvio = evento.IdEnvio,
            FechaEvento = evento.FechaEvento,
            FechaRecepcion = RelojLima.Ahora(),
            Procesado = false,
            Contenido = evento.Contenido
        };

        _db.EventosRecibidos.Add(entidad);
        await _db.SaveChangesAsync();

        return entidad.IdEventoRecibido;
    }

    public async Task MarcarComoProcesado(int idEventoRecibido)
    {
        var entidad = await _db.EventosRecibidos.FindAsync(idEventoRecibido);
        if (entidad is not null)
        {
            entidad.Procesado = true;
            await _db.SaveChangesAsync();
        }
    }
}
