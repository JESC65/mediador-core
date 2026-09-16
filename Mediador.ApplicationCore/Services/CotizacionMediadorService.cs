using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Interfaces;
using Mediador.ApplicationCore.Utilidades;

namespace Mediador.ApplicationCore.Services;

public class CotizacionMediadorService : ICotizacionMediadorService
{
    private readonly IEnvioRepository _envioRepositorio;
    private readonly ITransportistaCotizableRepository _transportistaRepositorio;
    private readonly ICotizacionRepository _cotizacionRepositorio;

    public CotizacionMediadorService(
        IEnvioRepository envioRepositorio,
        ITransportistaCotizableRepository transportistaRepositorio,
        ICotizacionRepository cotizacionRepositorio)
    {
        _envioRepositorio = envioRepositorio;
        _transportistaRepositorio = transportistaRepositorio;
        _cotizacionRepositorio = cotizacionRepositorio;
    }

    public async Task<ResultadoCotizacionDto> Cotizar(CotizarRequestDto request)
    {
        var existente = await _envioRepositorio.BuscarPorIdCorrelacion(request.IdCorrelacion);
        if (existente is not null)
        {
            if (existente.Estado != "COTIZADO")
            {
                return ResultadoCotizacionDto.Conflicto(existente.Estado);
            }

            var opcionesPrevias = await _cotizacionRepositorio.ObtenerNoDescartadas(existente.IdEnvio);
            return ResultadoCotizacionDto.Ok(ArmarRespuestaConOpciones(
                request.IdCorrelacion, existente.PesoTotalKg, existente.CantidadPaquetes, opcionesPrevias));
        }

        var pesoTotalKg = request.Paquetes.Sum(p => p.PesoKg * p.Cantidad);
        var cantidadPaquetes = request.Paquetes.Sum(p => p.Cantidad);

        var idEnvio = await _envioRepositorio.CrearEnvioCotizado(request, pesoTotalKg, cantidadPaquetes);

        var idsConConvenio = await _transportistaRepositorio.ObtenerIdsConConvenioActivo(request.IdCourier);
        if (idsConConvenio.Count == 0)
        {
            return ResultadoCotizacionDto.Ok(SinOpciones(request.IdCorrelacion, "SIN_CONVENIO_VIGENTE"));
        }

        var candidatas = await _transportistaRepositorio.ObtenerCandidatasConRuta(
            request.IdCourier, request.UbigeoOrigen, request.UbigeoDestino);
        if (candidatas.Count == 0)
        {
            return ResultadoCotizacionDto.Ok(SinOpciones(request.IdCorrelacion, "NINGUNA_CUBRE_RUTA"));
        }

        var hayPaqueteFragil = request.Paquetes.Any(p => p.EsFragil);
        var vigenteHasta = RelojLima.Ahora().AddHours(2);
        var paraGuardar = new List<CotizacionParaGuardarDto>();

        foreach (var candidata in candidatas)
        {
            var fila = await EvaluarCandidata(candidata, request, pesoTotalKg, hayPaqueteFragil, idEnvio, vigenteHasta);
            paraGuardar.Add(fila);
        }

        await _cotizacionRepositorio.GuardarCotizaciones(paraGuardar);

        var opciones = paraGuardar
            .Where(c => !c.Descartada)
            .OrderBy(c => c.PrecioProveedor)
            .Select(MapearAOpcion)
            .ToList();

        if (opciones.Count == 0)
        {
            return ResultadoCotizacionDto.Ok(SinOpciones(request.IdCorrelacion, "NINGUNA_ACEPTA_CARGA"));
        }

        return ResultadoCotizacionDto.Ok(new CotizarResponseDto
        {
            IdCorrelacion = request.IdCorrelacion,
            Estado = "COTIZADO",
            PesoTotalKg = pesoTotalKg,
            CantidadPaquetes = cantidadPaquetes,
            Opciones = opciones
        });
    }

    private async Task<CotizacionParaGuardarDto> EvaluarCandidata(
        TransportistaCandidataDto candidata,
        CotizarRequestDto request,
        decimal pesoTotalKg,
        bool hayPaqueteFragil,
        int idEnvio,
        DateTimeOffset vigenteHasta)
    {
        var fila = new CotizacionParaGuardarDto
        {
            IdEnvio = idEnvio,
            IdTransportista = candidata.IdTransportista,
            NombreTransportista = candidata.RazonSocial,
            IdOpcion = GenerarIdOpcion(),
            PrecioEsEstimado = true,
            FrecuenciaSalidas = candidata.FrecuenciaSalidas,
            AceptaFragil = candidata.AceptaFragil,
            VigenteHasta = vigenteHasta
        };

        if (hayPaqueteFragil && !candidata.AceptaFragil)
        {
            fila.Descartada = true;
            fila.MotivoDescarte = "NO_ACEPTA_FRAGIL";
            return fila;
        }

        if (candidata.PesoMaximoKg.HasValue && pesoTotalKg > candidata.PesoMaximoKg.Value)
        {
            fila.Descartada = true;
            fila.MotivoDescarte = "EXCEDE_PESO_MAXIMO";
            return fila;
        }

        var tramo = await _transportistaRepositorio.BuscarTramo(candidata.IdRuta, request.TipoCarga, pesoTotalKg);
        if (tramo is null)
        {
            fila.Descartada = true;
            fila.MotivoDescarte = "SIN_TARIFA_APLICABLE";
            return fila;
        }

        fila.IdTarifaProveedor = tramo.IdTarifaProveedor;
        fila.PrecioProveedor = tramo.PrecioMinimo.HasValue
            ? Math.Max(tramo.Precio, tramo.PrecioMinimo.Value)
            : tramo.Precio;

        if (request.UbigeoOrigen is null)
        {
            fila.MotivoDescarte = "ORIGEN_NO_VERIFICADO";
        }

        return fila;
    }

    private static CotizarResponseDto SinOpciones(string idCorrelacion, string motivo) => new()
    {
        IdCorrelacion = idCorrelacion,
        Estado = "SIN_OPCIONES",
        Motivo = motivo
    };

    private static CotizarResponseDto ArmarRespuestaConOpciones(
        string idCorrelacion, decimal pesoTotalKg, int cantidadPaquetes, IReadOnlyList<OpcionCotizacionDto> opciones)
    {
        if (opciones.Count == 0)
        {
            return SinOpciones(idCorrelacion, "NINGUNA_ACEPTA_CARGA");
        }

        return new CotizarResponseDto
        {
            IdCorrelacion = idCorrelacion,
            Estado = "COTIZADO",
            PesoTotalKg = pesoTotalKg,
            CantidadPaquetes = cantidadPaquetes,
            Opciones = opciones.ToList()
        };
    }

    private static OpcionCotizacionDto MapearAOpcion(CotizacionParaGuardarDto c) => new()
    {
        IdOpcion = c.IdOpcion,
        IdTransportista = c.IdTransportista,
        NombreTransportista = c.NombreTransportista,
        PrecioReferencial = c.PrecioProveedor!.Value,
        PrecioEsEstimado = c.PrecioEsEstimado,
        FrecuenciaSalidas = c.FrecuenciaSalidas,
        AceptaFragil = c.AceptaFragil,
        VigenteHasta = c.VigenteHasta
    };

    private static string GenerarIdOpcion() =>
        "OPT-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
}
