using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Interfaces;
using Mediador.ApplicationCore.Utilidades;
using Mediador.Infrastructure.Data;
using Mediador.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Mediador.Infrastructure.Repositories;

public class PuntosRecepcionRepository : IPuntosRecepcionRepository
{
    private readonly MediadorDbContext _db;

    public PuntosRecepcionRepository(MediadorDbContext db)
    {
        _db = db;
    }

    public async Task<TransportistaInfoCapacidadesDto?> ObtenerTransportistaAsync(int idTransportista)
    {
        return await _db.Transportistas
            .AsNoTracking()
            .Where(t => t.IdTransportista == idTransportista)
            .Select(t => new TransportistaInfoCapacidadesDto
            {
                IdTransportista = t.IdTransportista,
                RazonSocial = t.RazonSocial,
                UrlConector = t.UrlConector,
                Activo = t.Activo
            })
            .FirstOrDefaultAsync();
    }

    public async Task<bool?> ObtenerCapacidadPuntosAsync(int idTransportista)
    {
        var cap = await _db.TransportistasCapacidades
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IdTransportista == idTransportista);

        return cap?.Puntos;
    }

    public async Task GuardarPuntosAsync(int idTransportista, IReadOnlyList<PuntoRecepcionDto> puntos)
    {
        var ahora = RelojLima.Ahora();

        foreach (var p in puntos)
        {
            var existente = await _db.PuntosRecepcion
                .FirstOrDefaultAsync(x => x.IdTransportista == idTransportista && x.CodigoExterno == p.Codigo);

            if (existente == null)
            {
                _db.PuntosRecepcion.Add(new PuntoRecepcion
                {
                    IdTransportista = idTransportista,
                    CodigoExterno = p.Codigo,
                    Nombre = p.Nombre,
                    Direccion = p.Direccion,
                    Ubigeo = p.Ubigeo,
                    Latitud = null,
                    Longitud = null,
                    Horario = null,
                    TiempoServicioMinutos = null,
                    Version = 1,
                    Activo = true,
                    FechaActualizacion = ahora
                });
            }
            else
            {
                bool cambio = existente.Nombre != p.Nombre ||
                              existente.Direccion != p.Direccion ||
                              existente.Ubigeo != p.Ubigeo;

                if (cambio)
                {
                    existente.Nombre = p.Nombre;
                    existente.Direccion = p.Direccion;
                    existente.Ubigeo = p.Ubigeo;
                    existente.Version += 1;
                    existente.FechaActualizacion = ahora;
                    // Mantiene intactos Latitud, Longitud, Horario si ya existían
                }
            }
        }

        await _db.SaveChangesAsync();
    }
}
