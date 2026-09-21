using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Interfaces;
using Mediador.ApplicationCore.Utilidades;
using Mediador.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Mediador.Infrastructure.Repositories;

public class RutaTarifaEventoRepository : IRutaTarifaEventoRepository
{
    private readonly MediadorDbContext _db;

    public RutaTarifaEventoRepository(MediadorDbContext db)
    {
        _db = db;
    }

    public async Task ActualizarOCrearRuta(int idTransportista, RutaEventoDto datos)
    {
        var ruta = await _db.Rutas.FirstOrDefaultAsync(r =>
            r.IdTransportista == idTransportista && r.CodigoRutaExterno == datos.IdRuta);

        var ahora = RelojLima.Ahora();

        if (ruta is null)
        {
            _db.Rutas.Add(new Models.Ruta
            {
                IdTransportista = idTransportista,
                CodigoRutaExterno = datos.IdRuta,
                UbigeoOrigen = datos.UbigeoOrigen,
                UbigeoDestino = datos.UbigeoDestino,
                NombreOrigen = datos.NombreOrigen,
                NombreDestino = datos.NombreDestino,
                FrecuenciaSalidas = datos.FrecuenciaSalidas,
                HoraSalidaHabitual = datos.HoraSalidaHabitual,
                AceptaFragil = datos.AceptaFragil,
                PesoMaximoKg = datos.PesoMaximoKg,
                Activa = datos.Activa,
                FechaActualizacion = ahora
            });
        }
        else
        {
            ruta.UbigeoOrigen = datos.UbigeoOrigen;
            ruta.UbigeoDestino = datos.UbigeoDestino;
            ruta.NombreOrigen = datos.NombreOrigen;
            ruta.NombreDestino = datos.NombreDestino;
            ruta.FrecuenciaSalidas = datos.FrecuenciaSalidas;
            ruta.HoraSalidaHabitual = datos.HoraSalidaHabitual;
            ruta.AceptaFragil = datos.AceptaFragil;
            ruta.PesoMaximoKg = datos.PesoMaximoKg;
            ruta.Activa = datos.Activa;
            ruta.FechaActualizacion = ahora;
        }

        await _db.SaveChangesAsync();
    }

    public async Task ActualizarOCrearTarifa(int idTransportista, PrecioActualizadoDto datos)
    {
        var ruta = await _db.Rutas.AsNoTracking().FirstOrDefaultAsync(r =>
            r.IdTransportista == idTransportista && r.CodigoRutaExterno == datos.IdRuta);

        // Si la ruta todavía no existe del lado del Mediador, no hay dónde
        // colgar la tarifa. Debería haber llegado antes un RUTA_ACTUALIZADA,
        // o la ruta ya se sincronizó por B1. Se descarta en silencio.
        if (ruta is null)
        {
            return;
        }

        var tarifa = await _db.TarifasProveedor.FirstOrDefaultAsync(t =>
            t.IdRuta == ruta.IdRuta
            && t.TipoCarga == datos.TipoCarga
            && t.PesoDesdeKg == datos.PesoDesdeKg
            && t.PesoHastaKg == datos.PesoHastaKg);

        if (tarifa is null)
        {
            _db.TarifasProveedor.Add(new Models.TarifaProveedor
            {
                IdRuta = ruta.IdRuta,
                TipoCarga = datos.TipoCarga,
                PesoDesdeKg = datos.PesoDesdeKg,
                PesoHastaKg = datos.PesoHastaKg,
                Precio = datos.PrecioNuevo,
                VigenteDesde = DateOnly.FromDateTime(RelojLima.Ahora().Date),
                Activo = true
            });
        }
        else
        {
            tarifa.Precio = datos.PrecioNuevo;
        }

        await _db.SaveChangesAsync();
    }
}
