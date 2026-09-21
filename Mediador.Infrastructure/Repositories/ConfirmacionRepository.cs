using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Interfaces;
using Mediador.ApplicationCore.Utilidades;
using Mediador.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Mediador.Infrastructure.Repositories;

public class ConfirmacionRepository : IConfirmacionRepository
{
    private readonly MediadorDbContext _db;

    public ConfirmacionRepository(MediadorDbContext db)
    {
        _db = db;
    }

    public async Task<CotizacionParaConfirmarDto?> BuscarPorIdOpcion(string idOpcion)
    {
        var query =
            from cot in _db.Cotizaciones
            where cot.IdOpcion == idOpcion
            join envio in _db.Envios on cot.IdEnvio equals envio.IdEnvio
            join transportista in _db.Transportistas on cot.IdTransportista equals transportista.IdTransportista
            join tarifa in _db.TarifasProveedor on cot.IdTarifaProveedor equals tarifa.IdTarifaProveedor into tarifaJoin
            from tarifa in tarifaJoin.DefaultIfEmpty()
            join ruta in _db.Rutas on (tarifa != null ? tarifa.IdRuta : -1) equals ruta.IdRuta into rutaJoin
            from ruta in rutaJoin.DefaultIfEmpty()
            select new CotizacionParaConfirmarDto
            {
                IdCotizacion = cot.IdCotizacion,
                IdOpcion = cot.IdOpcion,
                IdEnvio = cot.IdEnvio,
                IdTarifaProveedor = cot.IdTarifaProveedor,
                PrecioProveedor = cot.PrecioProveedor,
                VigenteHasta = cot.VigenteHasta,
                IdTransportista = cot.IdTransportista,
                NombreTransportista = transportista.RazonSocial,
                UrlConector = transportista.UrlConector,
                CodigoRutaExterno = ruta != null ? ruta.CodigoRutaExterno : string.Empty,
                TipoCarga = envio.TipoCarga ?? string.Empty,
                CodigoSeguimientoEnvio = envio.CodigoSeguimiento
            };

        return await query.AsNoTracking().FirstOrDefaultAsync();
    }

    public async Task<CotizacionParaConfirmarDto?> ObtenerSiguienteAlternativa(int idEnvio, string idOpcionActual)
    {
        var query =
            from cot in _db.Cotizaciones
            where cot.IdEnvio == idEnvio && !cot.Descartada && cot.IdOpcion != idOpcionActual
            join envio in _db.Envios on cot.IdEnvio equals envio.IdEnvio
            join transportista in _db.Transportistas on cot.IdTransportista equals transportista.IdTransportista
            join tarifa in _db.TarifasProveedor on cot.IdTarifaProveedor equals tarifa.IdTarifaProveedor into tarifaJoin
            from tarifa in tarifaJoin.DefaultIfEmpty()
            join ruta in _db.Rutas on (tarifa != null ? tarifa.IdRuta : -1) equals ruta.IdRuta into rutaJoin
            from ruta in rutaJoin.DefaultIfEmpty()
            orderby cot.PrecioProveedor
            select new CotizacionParaConfirmarDto
            {
                IdCotizacion = cot.IdCotizacion,
                IdOpcion = cot.IdOpcion,
                IdEnvio = cot.IdEnvio,
                IdTarifaProveedor = cot.IdTarifaProveedor,
                PrecioProveedor = cot.PrecioProveedor,
                VigenteHasta = cot.VigenteHasta,
                IdTransportista = cot.IdTransportista,
                NombreTransportista = transportista.RazonSocial,
                UrlConector = transportista.UrlConector,
                CodigoRutaExterno = ruta != null ? ruta.CodigoRutaExterno : string.Empty,
                TipoCarga = envio.TipoCarga ?? string.Empty,
                CodigoSeguimientoEnvio = envio.CodigoSeguimiento
            };

        return await query.AsNoTracking().FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<PaqueteConfirmarDto>> ObtenerPaquetes(int idEnvio) =>
        await _db.EnvioPaquetes.AsNoTracking()
            .Where(p => p.IdEnvio == idEnvio)
            .Select(p => new PaqueteConfirmarDto
            {
                Descripcion = p.Descripcion,
                PesoKg = p.PesoKg,
                LargoCm = p.LargoCm,
                AnchoCm = p.AnchoCm,
                AltoCm = p.AltoCm,
                EsFragil = p.EsFragil ?? false,
                ValorDeclarado = p.ValorDeclarado
            })
            .ToListAsync();

    public async Task GuardarPersonas(int idEnvio, PersonaConfirmarDto remitente, PersonaConfirmarDto consignado)
    {
        await UpsertPersona(idEnvio, "REMITENTE", remitente);
        await UpsertPersona(idEnvio, "CONSIGNADO", consignado);
        await _db.SaveChangesAsync();
    }

    private async Task UpsertPersona(int idEnvio, string rol, PersonaConfirmarDto datos)
    {
        var entidad = await _db.EnvioPersonas.FirstOrDefaultAsync(p => p.IdEnvio == idEnvio && p.Rol == rol);
        if (entidad is null)
        {
            entidad = new Models.EnvioPersona { IdEnvio = idEnvio, Rol = rol };
            _db.EnvioPersonas.Add(entidad);
        }

        entidad.TipoDocumento = datos.TipoDocumento;
        entidad.NumeroDocumento = datos.NumeroDocumento;
        entidad.Nombres = datos.Nombres;
        entidad.Apellidos = datos.Apellidos;
        entidad.Telefono = datos.Telefono;
        entidad.Direccion = datos.Direccion;
        entidad.Ubigeo = datos.Ubigeo;
    }

    public async Task MarcarDescartada(int idCotizacion, string motivo)
    {
        var cotizacion = await _db.Cotizaciones.FirstAsync(c => c.IdCotizacion == idCotizacion);
        cotizacion.Descartada = true;
        cotizacion.MotivoDescarte = motivo;
        await _db.SaveChangesAsync();
    }

    public async Task<string> CrearCotizacionPorCambioPrecio(CotizacionParaConfirmarDto original, decimal precioReal, string idOpcionNueva)
    {
        var nueva = new Models.Cotizacion
        {
            IdEnvio = original.IdEnvio,
            IdTransportista = original.IdTransportista,
            IdTarifaProveedor = original.IdTarifaProveedor,
            IdOpcion = idOpcionNueva,
            PrecioProveedor = precioReal,
            PrecioEsEstimado = false,
            FechaCotizacion = RelojLima.Ahora(),
            VigenteHasta = original.VigenteHasta
        };

        _db.Cotizaciones.Add(nueva);
        await _db.SaveChangesAsync();

        return nueva.IdOpcion;
    }

    public async Task<string> GenerarSiguienteCodigoSeguimiento()
    {
        var anio = RelojLima.Ahora().Year;
        var prefijo = $"MED-{anio}-";

        // Nota: esto lee el máximo actual y no reserva el número con un
        // bloqueo — en un solo desarrollador probando no hay condición de
        // carrera real, pero si esto llega a producción con confirmaciones
        // concurrentes, conviene reemplazarlo por una secuencia de SQL Server.
        var existentes = await _db.Envios.AsNoTracking()
            .Where(e => e.CodigoSeguimiento != null && e.CodigoSeguimiento.StartsWith(prefijo))
            .Select(e => e.CodigoSeguimiento!)
            .ToListAsync();

        var siguienteNumero = existentes
            .Select(c => int.TryParse(c.AsSpan(prefijo.Length), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{prefijo}{siguienteNumero:D6}";
    }

    public async Task<bool> ConfirmarTransaccional(
        int idEnvio, int idCotizacion, int idTransportista, decimal precioFinal,
        string codigoSeguimiento, DateTimeOffset fechaConfirmacion)
    {
        await using var transaccion = await _db.Database.BeginTransactionAsync();
        try
        {
            var envio = await _db.Envios.FirstAsync(e => e.IdEnvio == idEnvio);
            envio.CodigoSeguimiento = codigoSeguimiento;
            envio.Estado = "CONFIRMADO";
            envio.IdTransportistaElegida = idTransportista;
            envio.IdCotizacionElegida = idCotizacion;
            envio.FechaConfirmacion = fechaConfirmacion;

            var cotizacion = await _db.Cotizaciones.FirstAsync(c => c.IdCotizacion == idCotizacion);
            cotizacion.Elegida = true;

            _db.EnvioEstados.Add(new Models.EnvioEstado
            {
                IdEnvio = idEnvio,
                Estado = "CONFIRMADO",
                FechaEstado = fechaConfirmacion,
                Origen = "MEDIADOR"
            });

            await _db.SaveChangesAsync();
            await transaccion.CommitAsync();
            return true;
        }
        catch
        {
            await transaccion.RollbackAsync();
            return false;
        }
    }

    public async Task GuardarPendienteConfirmacion(int idEnvio, string codigoSeguimiento, string? numeroGuiaEett)
    {
        var envio = await _db.Envios.FirstAsync(e => e.IdEnvio == idEnvio);
        envio.Estado = "PENDIENTE_CONFIRMACION";
        envio.CodigoSeguimiento = codigoSeguimiento;
        envio.Observacion = $"numeroGuiaEETT={numeroGuiaEett}";

        _db.EnvioEstados.Add(new Models.EnvioEstado
        {
            IdEnvio = idEnvio,
            Estado = "PENDIENTE_CONFIRMACION",
            FechaEstado = RelojLima.Ahora(),
            Origen = "MEDIADOR",
            Comentario = $"Registrado en transportista (guía {numeroGuiaEett}), falló guardar la confirmación"
        });

        await _db.SaveChangesAsync();
    }
}
