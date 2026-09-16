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
}
