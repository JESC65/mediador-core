using System.Text.Json;
using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Interfaces;
using Mediador.ApplicationCore.Utilidades;
using Mediador.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Mediador.Infrastructure.Repositories;

public class EnvioRepository : IEnvioRepository
{
    private readonly MediadorDbContext _db;

    public EnvioRepository(MediadorDbContext db)
    {
        _db = db;
    }

    public async Task<EnvioResumenDto?> BuscarPorIdCorrelacion(string idCorrelacion)
    {
        var envio = await _db.Envios.AsNoTracking()
            .FirstOrDefaultAsync(e => e.IdCorrelacion == idCorrelacion);

        if (envio is null)
        {
            return null;
        }

        return new EnvioResumenDto
        {
            IdEnvio = envio.IdEnvio,
            Estado = envio.Estado,
            PesoTotalKg = envio.PesoTotalKg,
            CantidadPaquetes = envio.CantidadPaquetes
        };
    }

    public async Task<int> CrearEnvioCotizado(CotizarRequestDto request, decimal pesoTotalKg, int cantidadPaquetes)
    {
        var ahora = RelojLima.Ahora();

        var envio = new Models.Envio
        {
            IdCorrelacion = request.IdCorrelacion,
            IdEnvioCourier = request.IdEnvioCourier,
            IdCourier = request.IdCourier,
            UbigeoOrigen = request.UbigeoOrigen,
            UbigeoDestino = request.UbigeoDestino,
            TipoCarga = request.TipoCarga,
            PesoTotalKg = pesoTotalKg,
            CantidadPaquetes = cantidadPaquetes,
            Estado = "COTIZADO",
            FechaRegistro = ahora
        };
        _db.Envios.Add(envio);

        // Se guarda primero para tener el IdEnvio (identity) que necesitan
        // los paquetes y el estado inicial.
        await _db.SaveChangesAsync();

        foreach (var paquete in request.Paquetes)
        {
            _db.EnvioPaquetes.Add(new Models.EnvioPaquete
            {
                IdEnvio = envio.IdEnvio,
                Descripcion = paquete.Descripcion,
                PesoKg = paquete.PesoKg,
                Cantidad = paquete.Cantidad,
                EsFragil = paquete.EsFragil
            });
        }

        _db.EnvioEstados.Add(new Models.EnvioEstado
        {
            IdEnvio = envio.IdEnvio,
            Estado = "COTIZADO",
            FechaEstado = ahora,
            Origen = "MEDIADOR"
        });

        await _db.SaveChangesAsync();

        return envio.IdEnvio;
    }
    public async Task<EnvioEventoDto?> BuscarPorCodigoSeguimiento(string codigoSeguimiento)
    {
        var envio = await _db.Envios.AsNoTracking()
            .FirstOrDefaultAsync(e => e.CodigoSeguimiento == codigoSeguimiento);

        if (envio is null)
        {
            return null;
        }

        return new EnvioEventoDto { IdEnvio = envio.IdEnvio, Estado = envio.Estado };
    }

    public async Task ActualizarEstado(int idEnvio, string nuevoEstado, string origen, string? comentario)
    {
        var envio = await _db.Envios.FirstAsync(e => e.IdEnvio == idEnvio);
        envio.Estado = nuevoEstado;

        _db.EnvioEstados.Add(new Models.EnvioEstado
        {
            IdEnvio = idEnvio,
            Estado = nuevoEstado,
            FechaEstado = RelojLima.Ahora(),
            Origen = origen,
            Comentario = comentario
        });

        await _db.SaveChangesAsync();
    }

    public async Task<EnvioConsultaDto?> ObtenerConsulta(string codigoSeguimiento)
    {
        var envio = await _db.Envios.AsNoTracking()
            .FirstOrDefaultAsync(e => e.CodigoSeguimiento == codigoSeguimiento);

        if (envio is null)
        {
            return null;
        }

        string? nombreTransportista = null;
        decimal? precioAcordado = null;

        // Hasta que exista el bloque de confirmación, estos dos campos van
        // a quedar en null para todos los envíos: recién ahí se llenan
        // IdTransportistaElegida / IdCotizacionElegida.
        if (envio.IdTransportistaElegida.HasValue)
        {
            nombreTransportista = await _db.Transportistas.AsNoTracking()
                .Where(t => t.IdTransportista == envio.IdTransportistaElegida.Value)
                .Select(t => t.RazonSocial)
                .FirstOrDefaultAsync();
        }

        if (envio.IdCotizacionElegida.HasValue)
        {
            precioAcordado = await _db.Cotizaciones.AsNoTracking()
                .Where(c => c.IdCotizacion == envio.IdCotizacionElegida.Value)
                .Select(c => c.PrecioProveedor)
                .FirstOrDefaultAsync();
        }

        var historial = await _db.EnvioEstados.AsNoTracking()
            .Where(e => e.IdEnvio == envio.IdEnvio)
            .OrderBy(e => e.FechaEstado)
            .Select(e => new HistorialEstadoDto
            {
                Estado = e.Estado,
                Fecha = e.FechaEstado,
                Origen = e.Origen,
                Comentario = e.Comentario
            })
            .ToListAsync();

        // busAsignado sale del último evento BUS_ASIGNADO recibido para este
        // envío, no de una columna propia — se lee del Contenido guardado en
        // MED.EventoRecibido para no depender del formato de texto libre del
        // comentario del historial.
        string? busAsignado = null;
        var contenidoEventoBus = await _db.EventosRecibidos.AsNoTracking()
            .Where(ev => ev.IdEnvio == envio.IdEnvio && ev.TipoEvento == "BUS_ASIGNADO")
            .OrderByDescending(ev => ev.FechaRecepcion)
            .Select(ev => ev.Contenido)
            .FirstOrDefaultAsync();

        if (contenidoEventoBus is not null)
        {
            using var doc = JsonDocument.Parse(contenidoEventoBus);
            if (doc.RootElement.TryGetProperty("datos", out var datos)
                && datos.TryGetProperty("placaBus", out var placa))
            {
                busAsignado = placa.GetString();
            }
        }

        return new EnvioConsultaDto
        {
            CodigoSeguimiento = envio.CodigoSeguimiento!,
            EstadoActual = envio.Estado,
            IdTransportista = envio.IdTransportistaElegida,
            NombreTransportista = nombreTransportista,
            PrecioAcordado = precioAcordado,
            BusAsignado = busAsignado,
            Historial = historial
        };
    }
}
