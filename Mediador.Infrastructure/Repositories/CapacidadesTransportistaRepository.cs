using System.Text.Json;
using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Interfaces;
using Mediador.ApplicationCore.Utilidades;
using Mediador.Infrastructure.Data;
using Mediador.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Mediador.Infrastructure.Repositories;

public class CapacidadesTransportistaRepository : ICapacidadesTransportistaRepository
{
    private readonly MediadorDbContext _db;

    public CapacidadesTransportistaRepository(MediadorDbContext db)
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

    public async Task<IReadOnlyList<TransportistaInfoCapacidadesDto>> ObtenerActivasConUrlAsync()
    {
        return await _db.Transportistas
            .AsNoTracking()
            .Where(t => t.Activo && !string.IsNullOrWhiteSpace(t.UrlConector))
            .Select(t => new TransportistaInfoCapacidadesDto
            {
                IdTransportista = t.IdTransportista,
                RazonSocial = t.RazonSocial,
                UrlConector = t.UrlConector,
                Activo = t.Activo
            })
            .ToListAsync();
    }

    public async Task GuardarCapacidadesAsync(int idTransportista, CapacidadesResponseDto capacidades)
    {
        var entidad = await _db.TransportistasCapacidades
            .FirstOrDefaultAsync(c => c.IdTransportista == idTransportista);

        var jsonEventos = capacidades.Eventos != null && capacidades.Eventos.Count > 0
            ? JsonSerializer.Serialize(capacidades.Eventos)
            : null;

        var ahora = RelojLima.Ahora();

        if (entidad == null)
        {
            entidad = new TransportistaCapacidad
            {
                IdTransportista = idTransportista,
                Familia = capacidades.Familia,
                VersionConector = capacidades.VersionConector,
                VersionContrato = capacidades.VersionContrato,
                PrecioEnLinea = capacidades.Operaciones?.PrecioEnLinea ?? true,
                RegistroEnLinea = capacidades.Operaciones?.RegistroEnLinea ?? true,
                Anulacion = capacidades.Operaciones?.Anulacion ?? true,
                Puntos = capacidades.Operaciones?.Puntos ?? true,
                RecepcionPorBulto = capacidades.RecepcionPorBulto,
                EventosJson = jsonEventos,
                FechaActualizacion = ahora
            };
            _db.TransportistasCapacidades.Add(entidad);
        }
        else
        {
            entidad.Familia = capacidades.Familia;
            entidad.VersionConector = capacidades.VersionConector;
            entidad.VersionContrato = capacidades.VersionContrato;
            entidad.PrecioEnLinea = capacidades.Operaciones?.PrecioEnLinea ?? true;
            entidad.RegistroEnLinea = capacidades.Operaciones?.RegistroEnLinea ?? true;
            entidad.Anulacion = capacidades.Operaciones?.Anulacion ?? true;
            entidad.Puntos = capacidades.Operaciones?.Puntos ?? true;
            entidad.RecepcionPorBulto = capacidades.RecepcionPorBulto;
            entidad.EventosJson = jsonEventos;
            entidad.FechaActualizacion = ahora;
        }

        await _db.SaveChangesAsync();
    }
}
