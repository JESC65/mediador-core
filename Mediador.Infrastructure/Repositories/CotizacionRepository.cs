using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Interfaces;
using Mediador.ApplicationCore.Utilidades;
using Mediador.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Mediador.Infrastructure.Repositories;

public class CotizacionRepository : ICotizacionRepository
{
    private readonly MediadorDbContext _db;

    public CotizacionRepository(MediadorDbContext db)
    {
        _db = db;
    }

    public async Task GuardarCotizaciones(IReadOnlyList<CotizacionParaGuardarDto> cotizaciones)
    {
        var ahora = RelojLima.Ahora();

        foreach (var c in cotizaciones)
        {
            _db.Cotizaciones.Add(new Models.Cotizacion
            {
                IdEnvio = c.IdEnvio,
                IdTransportista = c.IdTransportista,
                IdTarifaProveedor = c.IdTarifaProveedor,
                IdOpcion = c.IdOpcion,
                PrecioProveedor = c.PrecioProveedor,
                PrecioEsEstimado = c.PrecioEsEstimado,
                FrecuenciaSalidas = c.FrecuenciaSalidas,
                Descartada = c.Descartada,
                MotivoDescarte = c.MotivoDescarte,
                FechaCotizacion = ahora,
                VigenteHasta = c.VigenteHasta
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<OpcionCotizacionDto>> ObtenerNoDescartadas(int idEnvio)
    {
        // Se pasa por TarifaProveedor -> Ruta para recuperar AceptaFragil:
        // Cotizacion no guarda ese dato directo, y toda fila no descartada
        // sí tiene IdTarifaProveedor (se calculó un precio para llegar hasta acá).
        var query =
            from cot in _db.Cotizaciones
            where cot.IdEnvio == idEnvio && !cot.Descartada
            join transportista in _db.Transportistas
                on cot.IdTransportista equals transportista.IdTransportista
            join tarifa in _db.TarifasProveedor
                on cot.IdTarifaProveedor equals tarifa.IdTarifaProveedor
            join ruta in _db.Rutas
                on tarifa.IdRuta equals ruta.IdRuta
            orderby cot.PrecioProveedor
            select new OpcionCotizacionDto
            {
                IdOpcion = cot.IdOpcion,
                IdTransportista = cot.IdTransportista,
                NombreTransportista = transportista.RazonSocial,
                PrecioReferencial = cot.PrecioProveedor ?? 0,
                PrecioEsEstimado = cot.PrecioEsEstimado,
                FrecuenciaSalidas = cot.FrecuenciaSalidas,
                AceptaFragil = ruta.AceptaFragil,
                VigenteHasta = cot.VigenteHasta
            };

        return await query.AsNoTracking().ToListAsync();
    }
}
