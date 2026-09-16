using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Interfaces;
using Mediador.ApplicationCore.Utilidades;
using Mediador.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Mediador.Infrastructure.Repositories;

public class TransportistaCotizableRepository : ITransportistaCotizableRepository
{
    private readonly MediadorDbContext _db;

    public TransportistaCotizableRepository(MediadorDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<int>> ObtenerIdsConConvenioActivo(int idCourier)
    {
        var hoy = DateOnly.FromDateTime(RelojLima.Ahora().Date);

        return await _db.Convenios.AsNoTracking()
            .Where(c => c.IdCourier == idCourier && c.Activo && (c.FechaFin == null || c.FechaFin >= hoy))
            .Select(c => c.IdTransportista)
            .Distinct()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TransportistaCandidataDto>> ObtenerCandidatasConRuta(
        int idCourier, string? ubigeoOrigen, string ubigeoDestino)
    {
        var hoy = DateOnly.FromDateTime(RelojLima.Ahora().Date);

        var query =
            from convenio in _db.Convenios
            where convenio.IdCourier == idCourier
                  && convenio.Activo
                  && (convenio.FechaFin == null || convenio.FechaFin >= hoy)
            join transportista in _db.Transportistas
                on convenio.IdTransportista equals transportista.IdTransportista
            where transportista.Activo
            join ruta in _db.Rutas
                on transportista.IdTransportista equals ruta.IdTransportista
            where ruta.Activa
                  && ruta.UbigeoDestino == ubigeoDestino
                  && (ubigeoOrigen == null || ruta.UbigeoOrigen == ubigeoOrigen)
            select new TransportistaCandidataDto
            {
                IdTransportista = transportista.IdTransportista,
                RazonSocial = transportista.RazonSocial,
                IdRuta = ruta.IdRuta,
                AceptaFragil = ruta.AceptaFragil,
                PesoMaximoKg = ruta.PesoMaximoKg,
                FrecuenciaSalidas = ruta.FrecuenciaSalidas
            };

        return await query.AsNoTracking().ToListAsync();
    }

    public async Task<TarifaTramoDto?> BuscarTramo(int idRuta, string tipoCarga, decimal pesoTotalKg)
    {
        var tarifa = await _db.TarifasProveedor.AsNoTracking()
            .Where(t => t.IdRuta == idRuta
                        && t.TipoCarga == tipoCarga
                        && t.Activo
                        && pesoTotalKg >= t.PesoDesdeKg
                        && pesoTotalKg <= t.PesoHastaKg)
            .FirstOrDefaultAsync();

        if (tarifa is null)
        {
            return null;
        }

        return new TarifaTramoDto
        {
            IdTarifaProveedor = tarifa.IdTarifaProveedor,
            Precio = tarifa.Precio,
            PrecioMinimo = tarifa.PrecioMinimo
        };
    }
}
