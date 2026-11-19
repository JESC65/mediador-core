using System.Collections.Concurrent;
using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Dtos.Simulador;
using Mediador.ApplicationCore.Interfaces;
using Mediador.ApplicationCore.Utilidades;
using Microsoft.Extensions.Options;

namespace Mediador.ApplicationCore.Services;

public class ConfirmacionMediadorService : IConfirmacionMediadorService
{
    // Candado en memoria por IdEnvio: evita que dos confirmaciones casi
    // simultáneas del mismo envío pasen ambas los pasos 1-7 a la vez.
    // Vale solo mientras haya UNA sola instancia del Mediador corriendo —
    // si algún día hay varias instancias detrás de un balanceador, esto
    // tiene que pasar a un candado en base de datos (ej. sp_getapplock).
    // El diccionario crece con cada envío nuevo y nunca se limpia; para el
    // volumen actual no es un problema, pero queda anotado.
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> _candados = new();

    private readonly IConfirmacionRepository _confirmacionRepositorio;
    private readonly ISimuladorTransportistaClient _simulador;
    private readonly ParametrosOptions _parametros;

    public ConfirmacionMediadorService(
        IConfirmacionRepository confirmacionRepositorio,
        ISimuladorTransportistaClient simulador,
        IOptions<ParametrosOptions> parametros)
    {
        _confirmacionRepositorio = confirmacionRepositorio;
        _simulador = simulador;
        _parametros = parametros.Value;
    }

    public async Task<ConfirmarResponseDto?> Confirmar(ConfirmarRequestDto request)
    {
        // Primera lectura: solo para saber cuál es el IdEnvio y así poder
        // ubicar el candado correcto. Si el idOpcion no existe, no hay nada
        // que bloquear.
        var candidataInicial = await _confirmacionRepositorio.BuscarPorIdOpcion(request.IdOpcion);
        if (candidataInicial is null)
        {
            return null; // el controlador responde 404
        }

        var candado = _candados.GetOrAdd(candidataInicial.IdEnvio, _ => new SemaphoreSlim(1, 1));
        await candado.WaitAsync();
        try
        {
            // Se vuelve a leer AQUÍ, ya con el candado tomado. Si esta
            // petición esperó porque otra confirmación del mismo envío iba
            // primero, esta relectura ya ve el resultado de esa otra
            // (CodigoSeguimiento puesto, si terminó bien) — la lectura de
            // arriba, en cambio, pudo haber sido tomada antes de que la
            // otra terminara, y por eso no basta.
            var candidata = await _confirmacionRepositorio.BuscarPorIdOpcion(request.IdOpcion) ?? candidataInicial;
            return await EjecutarConfirmacion(request, candidata);
        }
        finally
        {
            candado.Release();
        }
    }

    private async Task<ConfirmarResponseDto> EjecutarConfirmacion(ConfirmarRequestDto request, CotizacionParaConfirmarDto candidataEncontrada)
    {
        // Nullable a partir de acá: el bucle de abajo la reasigna a null cuando
        // se agotan las alternativas (ObtenerSiguienteAlternativa devuelve null).
        CotizacionParaConfirmarDto? candidata = candidataEncontrada;

        // Paso 1 — Idempotencia: si el envío de esta opción ya tiene código y está CONFIRMADO,
        // no se crea nada nuevo. Si quedó en PENDIENTE_CONFIRMACION, no se devuelve YA_CONFIRMADO,
        // sino que se continúa el flujo para reintentar el registro en la misma transportista.
        if (candidata.CodigoSeguimientoEnvio is not null && candidata.EstadoEnvio == "CONFIRMADO")
        {
            return new ConfirmarResponseDto
            {
                IdCorrelacion = request.IdCorrelacion,
                Estado = "YA_CONFIRMADO",
                CodigoSeguimiento = candidata.CodigoSeguimientoEnvio,
                IdTransportista = candidata.IdTransportista,
                NombreTransportista = candidata.NombreTransportista,
                PrecioFinal = candidata.PrecioProveedor
            };
        }

        // Paso 2 — Vigencia (solo aplica a cotizaciones nuevas, no a reintentos de PENDIENTE_CONFIRMACION).
        if (candidata.EstadoEnvio != "PENDIENTE_CONFIRMACION" && RelojLima.Ahora() > candidata.VigenteHasta)
        {
            return new ConfirmarResponseDto
            {
                IdCorrelacion = request.IdCorrelacion,
                Estado = "OPCION_EXPIRADA"
            };
        }

        // Paso 3 — Guardar remitente y consignado. Va antes de llamar al simulador
        // (así lo pidió Rodrigo hoy), aunque la confirmación todavía pueda no cerrarse.
        await _confirmacionRepositorio.GuardarPersonas(candidata.IdEnvio, request.Remitente, request.Consignado);

        var paquetes = await _confirmacionRepositorio.ObtenerPaquetes(candidata.IdEnvio);

        // Pasos 4 a 7, con soporte para "agotar reintentos -> descartar -> ofrecer la siguiente opción".
        while (true)
        {
            if (candidata is null)
            {
                return new ConfirmarResponseDto
                {
                    IdCorrelacion = request.IdCorrelacion,
                    Estado = "SIN_OPCIONES_DISPONIBLES",
                    Motivo = "Se agotaron todas las opciones no descartadas para este envío."
                };
            }

            if (string.IsNullOrWhiteSpace(candidata.UrlConector))
            {
                await _confirmacionRepositorio.MarcarDescartada(candidata.IdCotizacion, "SIN_RESPUESTA");
                candidata = await _confirmacionRepositorio.ObtenerSiguienteAlternativa(candidata.IdEnvio, candidata.IdOpcion);
                continue;
            }

            // Paso 4 — Consultar el precio real (B3).
            // Si candidata.PrecioEnLinea == true: flujo actual con B3.
            // Si candidata.PrecioEnLinea == false: confirmar con precio estimado (sin B3).
            decimal precioReal;
            bool precioEsEstimado;

            if (candidata.PrecioEnLinea)
            {
                precioEsEstimado = false;

                // R5: idCorrelacion único y sin fecha, generado y guardado en MED.Envio antes de la primera llamada.
                var idCorrelacionPrecio = candidata.IdCorrelacionPrecio;
                if (string.IsNullOrWhiteSpace(idCorrelacionPrecio))
                {
                    idCorrelacionPrecio = await _confirmacionRepositorio.AsegurarIdCorrelacionPrecio(candidata.IdEnvio);
                    candidata.IdCorrelacionPrecio = idCorrelacionPrecio;
                }

                var solicitudPrecio = new SimuladorCotizarRequestDto
                {
                    IdCorrelacion = idCorrelacionPrecio,
                    IdRuta = candidata.CodigoRutaExterno,
                    TipoCarga = candidata.TipoCarga,
                    Paquetes = paquetes
                        .SelectMany(p => Enumerable.Repeat(p, Math.Max(p.Cantidad, 1)))
                        .Select(p => new SimuladorPaqueteCotizarDto
                        {
                            PesoKg = p.PesoKg,
                            LargoCm = p.LargoCm,
                            AnchoCm = p.AnchoCm,
                            AltoCm = p.AltoCm,
                            EsFragil = p.EsFragil
                        }).ToList()
                };

                var resultadoPrecio = await LlamarConReintentos(
                    () => _simulador.ConsultarPrecio(candidata.UrlConector!, solicitudPrecio));

                if (!resultadoPrecio.Exitosa)
                {
                    await _confirmacionRepositorio.MarcarDescartada(candidata.IdCotizacion, "SIN_RESPUESTA");
                    candidata = await _confirmacionRepositorio.ObtenerSiguienteAlternativa(candidata.IdEnvio, candidata.IdOpcion);
                    continue;
                }

                var respuestaPrecio = resultadoPrecio.Respuesta!;

                if (!respuestaPrecio.Disponible)
                {
                    await _confirmacionRepositorio.MarcarDescartada(candidata.IdCotizacion, respuestaPrecio.Motivo ?? "NO_DISPONIBLE");
                    candidata = await _confirmacionRepositorio.ObtenerSiguienteAlternativa(candidata.IdEnvio, candidata.IdOpcion);
                    continue;
                }

                precioReal = respuestaPrecio.Precio ?? 0;

                if (precioReal != candidata.PrecioProveedor)
                {
                    // Formato tomado literalmente del ejemplo de A2 en el contrato: "OPT-B2C9-R1".
                    var idOpcionNueva = $"{candidata.IdOpcion}-R1";
                    await _confirmacionRepositorio.CrearCotizacionPorCambioPrecio(candidata, precioReal, idOpcionNueva);

                    return new ConfirmarResponseDto
                    {
                        IdCorrelacion = request.IdCorrelacion,
                        Estado = "PRECIO_CAMBIO",
                        PrecioReferencial = candidata.PrecioProveedor,
                        PrecioReal = precioReal,
                        RequiereConfirmacion = true,
                        IdOpcionActualizada = idOpcionNueva
                    };
                }
            }
            else
            {
                // Sin B3: usar directamente candidata.PrecioProveedor como precioReal
                precioReal = candidata.PrecioProveedor ?? 0m;
                precioEsEstimado = true;
            }

            // Paso 6 (adelantado) — si ya tenía código asignado (reintento de PENDIENTE_CONFIRMACION),
            // se reutiliza; si no, se genera el correlativo acá porque B4 lo necesita en el
            // cuerpo de la petición; recién se persiste de verdad en el paso 7.
            var codigoSeguimiento = candidata.CodigoSeguimientoEnvio
                ?? await _confirmacionRepositorio.GenerarSiguienteCodigoSeguimiento();

            // Paso 5 — Registrar en la transportista (B4).
            // R5: idCorrelacion único y sin fecha para B4, generado y guardado en MED.Envio antes de la llamada.
            var idCorrelacionRegistro = candidata.IdCorrelacionRegistro;
            if (string.IsNullOrWhiteSpace(idCorrelacionRegistro))
            {
                idCorrelacionRegistro = await _confirmacionRepositorio.AsegurarIdCorrelacionRegistro(candidata.IdEnvio);
                candidata.IdCorrelacionRegistro = idCorrelacionRegistro;
            }

            var solicitudRegistro = new SimuladorRegistrarRequestDto
            {
                IdCorrelacion = idCorrelacionRegistro,
                CodigoSeguimiento = codigoSeguimiento,
                IdRuta = candidata.CodigoRutaExterno,
                PrecioAcordado = precioReal, // nunca el precio de venta al cliente final
                Paquetes = paquetes
                    .SelectMany(p => Enumerable.Repeat(p, Math.Max(p.Cantidad, 1)))
                    .Select(p => new SimuladorPaqueteRegistroDto
                    {
                        Descripcion = p.Descripcion ?? string.Empty,
                        PesoKg = p.PesoKg,
                        EsFragil = p.EsFragil,
                        ValorDeclarado = p.ValorDeclarado
                    }).ToList(),
                Remitente = MapearPersona(request.Remitente),
                Consignado = MapearPersona(request.Consignado)
            };

            var resultadoRegistro = await LlamarConReintentos(
                () => _simulador.Registrar(candidata.UrlConector!, solicitudRegistro));

            if (!resultadoRegistro.Exitosa)
            {
                // Si B4 no respondió dentro de t_registro_eett tras agotar r_esperas_sincronas,
                // NUNCA descartar la opción ni pasar a otra transportista: la primera transportista
                // pudo haber registrado el envío antes del timeout/falla de red.
                await _confirmacionRepositorio.GuardarPendienteConfirmacion(candidata.IdEnvio, codigoSeguimiento, null);

                return new ConfirmarResponseDto
                {
                    IdCorrelacion = request.IdCorrelacion,
                    Estado = "PENDIENTE_CONFIRMACION",
                    CodigoSeguimiento = codigoSeguimiento,
                    IdTransportista = candidata.IdTransportista,
                    NombreTransportista = candidata.NombreTransportista,
                    Motivo = "La transportista no respondió al registro dentro del tiempo esperado. Reintente la confirmación."
                };
            }

            var respuestaRegistro = resultadoRegistro.Respuesta!;

            if (!respuestaRegistro.Aceptado)
            {
                await _confirmacionRepositorio.MarcarDescartada(candidata.IdCotizacion, respuestaRegistro.Motivo ?? "OPCION_NO_DISPONIBLE");
                candidata = await _confirmacionRepositorio.ObtenerSiguienteAlternativa(candidata.IdEnvio, candidata.IdOpcion);
                continue;
            }

            // Paso 7 — Transacción. Ya se registró del lado de la transportista: de acá
            // en más, numeroGuiaEETT no se puede perder pase lo que pase.
            var fechaConfirmacion = RelojLima.Ahora();
            var exito = await _confirmacionRepositorio.ConfirmarTransaccional(
                candidata.IdEnvio, candidata.IdCotizacion, candidata.IdTransportista,
                precioReal, codigoSeguimiento, fechaConfirmacion, precioEsEstimado);

            if (!exito)
            {
                await _confirmacionRepositorio.GuardarPendienteConfirmacion(
                    candidata.IdEnvio, codigoSeguimiento, respuestaRegistro.NumeroGuiaEETT);

                return new ConfirmarResponseDto
                {
                    IdCorrelacion = request.IdCorrelacion,
                    Estado = "PENDIENTE_CONFIRMACION",
                    CodigoSeguimiento = codigoSeguimiento,
                    NumeroGuiaEett = respuestaRegistro.NumeroGuiaEETT,
                    Motivo = "Se registró en la transportista pero falló guardar la confirmación. Revisar manualmente."
                };
            }

            return new ConfirmarResponseDto
            {
                IdCorrelacion = request.IdCorrelacion,
                Estado = "CONFIRMADO",
                CodigoSeguimiento = codigoSeguimiento,
                IdTransportista = candidata.IdTransportista,
                NombreTransportista = candidata.NombreTransportista,
                PrecioFinal = precioReal,
                PrecioCambio = false,
                PrecioEsEstimado = precioEsEstimado,
                FechaConfirmacion = fechaConfirmacion
            };
        }
    }

    /// <summary>
    /// Reintenta solo fallas de comunicación (3.3) — una respuesta de negocio
    /// (disponible: false, aceptado: false) no es un motivo para reintentar.
    /// </summary>
    private async Task<ResultadoLlamadaSimuladorDto<TRespuesta>> LlamarConReintentos<TRespuesta>(
        Func<Task<ResultadoLlamadaSimuladorDto<TRespuesta>>> llamada)
    {
        var resultado = await llamada();

        foreach (var espera in _parametros.r_esperas_sincronas)
        {
            if (resultado.Exitosa)
            {
                break;
            }

            await Task.Delay(espera);
            resultado = await llamada();
        }

        return resultado;
    }

    private static SimuladorPersonaDto MapearPersona(PersonaConfirmarDto persona) => new()
    {
        TipoDocumento = persona.TipoDocumento,
        NumeroDocumento = persona.NumeroDocumento,
        Nombres = persona.Nombres,
        Apellidos = persona.Apellidos,
        Telefono = persona.Telefono,
        Direccion = persona.Direccion,
        Ubigeo = persona.Ubigeo
    };
}
