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

    public async Task<EventoExistenteDto?> ObtenerEventoExistente(string idEvento)
    {
        return await _db.EventosRecibidos
            .AsNoTracking()
            .Where(e => e.IdEvento == idEvento && !e.EsConflicto)
            .OrderBy(e => e.IdEventoRecibido)
            .Select(e => new EventoExistenteDto
            {
                IdEventoRecibido = e.IdEventoRecibido,
                IdEvento = e.IdEvento,
                HuellaCuerpo = e.HuellaCuerpo,
                Contenido = e.Contenido,
                Procesado = e.Procesado,
                EsConflicto = e.EsConflicto,
                RequiereMensajeRechazado = e.RequiereMensajeRechazado
            })
            .FirstOrDefaultAsync();
    }

    public async Task<int> GuardarEvento(EventoParaGuardarDto evento)
    {
        var entidad = new Models.EventoRecibido
        {
            IdEvento = evento.IdEvento,
            TipoEvento = evento.TipoEvento,
            Secuencia = evento.Secuencia,
            VersionContrato = evento.VersionContrato,
            EntidadTipo = evento.EntidadTipo,
            EntidadId = evento.EntidadId,
            VersionEntidad = evento.VersionEntidad,
            Emisor = evento.Emisor,
            IdTransportista = evento.IdTransportista,
            IdEnvio = evento.IdEnvio,
            FechaEvento = evento.FechaEvento,
            FechaRecepcion = RelojLima.Ahora(),
            Procesado = false,
            Contenido = evento.Contenido,
            HuellaCuerpo = evento.HuellaCuerpo,
            RequiereMensajeRechazado = evento.RequiereMensajeRechazado,
            MotivoRechazo = evento.MotivoRechazo,
            EsConflicto = evento.EsConflicto
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

    public async Task<int> ObtenerUltimaSecuenciaContigua(int idTransportista)
    {
        var registro = await _db.TransportistaSecuencias
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IdTransportista == idTransportista);
        return registro?.UltimaSecuenciaContigua ?? 0;
    }

    public async Task ActualizarUltimaSecuenciaContigua(int idTransportista, int secuencia)
    {
        var registro = await _db.TransportistaSecuencias
            .FirstOrDefaultAsync(s => s.IdTransportista == idTransportista);

        if (registro is null)
        {
            _db.TransportistaSecuencias.Add(new Models.TransportistaSecuencia
            {
                IdTransportista = idTransportista,
                UltimaSecuenciaContigua = secuencia,
                FechaActualizacion = RelojLima.Ahora()
            });
        }
        else
        {
            registro.UltimaSecuenciaContigua = secuencia;
            registro.FechaActualizacion = RelojLima.Ahora();
        }

        await _db.SaveChangesAsync();
    }

    public async Task RegistrarHueco(int idTransportista, int secuenciaEsperada, int secuenciaRecibida)
    {
        var yaExiste = await _db.SecuenciasHuecos
            .AnyAsync(h => h.IdTransportista == idTransportista
                        && h.SecuenciaEsperada == secuenciaEsperada
                        && h.SecuenciaRecibida == secuenciaRecibida
                        && !h.Resuelto);

        if (!yaExiste)
        {
            _db.SecuenciasHuecos.Add(new Models.SecuenciaHueco
            {
                IdTransportista = idTransportista,
                SecuenciaEsperada = secuenciaEsperada,
                SecuenciaRecibida = secuenciaRecibida,
                FechaDeteccion = RelojLima.Ahora(),
                Resuelto = false
            });
            await _db.SaveChangesAsync();
        }
    }

    public async Task<List<int>> ObtenerSecuenciasGuardadasMayoresA(int idTransportista, int secuencia)
    {
        return await _db.EventosRecibidos
            .AsNoTracking()
            .Where(e => e.IdTransportista == idTransportista && e.Secuencia.HasValue && e.Secuencia.Value > secuencia && !e.EsConflicto)
            .Select(e => e.Secuencia!.Value)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync();
    }

    public async Task ResolverHuecosHasta(int idTransportista, int secuenciaContigua)
    {
        var huecos = await _db.SecuenciasHuecos
            .Where(h => h.IdTransportista == idTransportista && !h.Resuelto && h.SecuenciaEsperada <= secuenciaContigua)
            .ToListAsync();

        if (huecos.Count > 0)
        {
            var ahora = RelojLima.Ahora();
            foreach (var h in huecos)
            {
                h.Resuelto = true;
                h.FechaResolucion = ahora;
            }
            await _db.SaveChangesAsync();
        }
    }
}
