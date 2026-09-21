using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Dtos.Simulador;
using Mediador.ApplicationCore.Interfaces;
using Mediador.ApplicationCore.Utilidades;

namespace Mediador.ApplicationCore.Services;

public class ConfirmacionMediadorService : IConfirmacionMediadorService
{
    // 3.3 — Tiempos de espera y reintentos. La espera máxima por intento ya
    // la aplica ISimuladorTransportistaClient (15s / 30s); acá solo se
    // orquesta CUÁNTAS veces reintentar y CUÁNTO esperar entre intentos.
    private static readonly int[] EsperasEntreReintentosSegundos = { 2, 5, 10 };

    private readonly IConfirmacionRepository _confirmacionRepositorio;
    private readonly ISimuladorTransportistaClient _simulador;

    public ConfirmacionMediadorService(
        IConfirmacionRepository confirmacionRepositorio,
        ISimuladorTransportistaClient simulador)
    {
        _confirmacionRepositorio = confirmacionRepositorio;
        _simulador = simulador;
    }

    public async Task<ConfirmarResponseDto?> Confirmar(ConfirmarRequestDto request)
    {
        var candidata = await _confirmacionRepositorio.BuscarPorIdOpcion(request.IdOpcion);
        if (candidata is null)
        {
            return null; // el controlador responde 404
        }

        // Paso 1 — Idempotencia: si el envío de esta opción ya tiene código, no se crea nada nuevo.
        // Simplificación: se informa con los datos de ESTA opción (transportista/precio), asumiendo
        // que es la misma que quedó elegida — es lo normal salvo que haya habido un PRECIO_CAMBIO
        // de por medio, caso en el que convendría reconsultar por codigoSeguimiento en vez de idOpcion.
        if (candidata.CodigoSeguimientoEnvio is not null)
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

        // Paso 2 — Vigencia.
        if (RelojLima.Ahora() > candidata.VigenteHasta)
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

            // Paso 4 — Consultar el precio real.
            var solicitudPrecio = new SimuladorCotizarRequestDto
            {
                IdCorrelacion = ArmarIdCorrelacionSimulador(candidata.IdEnvio),
                IdRuta = candidata.CodigoRutaExterno,
                TipoCarga = candidata.TipoCarga,
                Paquetes = paquetes.Select(p => new SimuladorPaqueteCotizarDto
                {
                    PesoKg = p.PesoKg,
                    LargoCm = p.LargoCm ?? 0,
                    AnchoCm = p.AnchoCm ?? 0,
                    AltoCm = p.AltoCm ?? 0,
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

            var precioReal = respuestaPrecio.Precio ?? 0;

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

            // Paso 6 (adelantado) — se genera el código acá porque B4 lo necesita en el
            // cuerpo de la petición; recién se persiste de verdad en el paso 7.
            var codigoSeguimiento = await _confirmacionRepositorio.GenerarSiguienteCodigoSeguimiento();

            // Paso 5 — Registrar en la transportista.
            var solicitudRegistro = new SimuladorRegistrarRequestDto
            {
                IdCorrelacion = solicitudPrecio.IdCorrelacion,
                CodigoSeguimiento = codigoSeguimiento,
                IdRuta = candidata.CodigoRutaExterno,
                PrecioAcordado = precioReal, // nunca el precio de venta al cliente final
                Paquetes = paquetes.Select(p => new SimuladorPaqueteRegistroDto
                {
                    Descripcion = p.Descripcion ?? string.Empty,
                    PesoKg = p.PesoKg,
                    EsFragil = p.EsFragil,
                    ValorDeclarado = p.ValorDeclarado ?? 0
                }).ToList(),
                Remitente = MapearPersona(request.Remitente),
                Consignado = MapearPersona(request.Consignado)
            };

            var resultadoRegistro = await LlamarConReintentos(
                () => _simulador.Registrar(candidata.UrlConector!, solicitudRegistro));

            if (!resultadoRegistro.Exitosa)
            {
                await _confirmacionRepositorio.MarcarDescartada(candidata.IdCotizacion, "SIN_RESPUESTA");
                candidata = await _confirmacionRepositorio.ObtenerSiguienteAlternativa(candidata.IdEnvio, candidata.IdOpcion);
                continue;
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
                precioReal, codigoSeguimiento, fechaConfirmacion);

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
                FechaConfirmacion = fechaConfirmacion
            };
        }
    }

    /// <summary>
    /// Reintenta solo fallas de comunicación (3.3) — una respuesta de negocio
    /// (disponible: false, aceptado: false) no es un motivo para reintentar.
    /// </summary>
    private static async Task<ResultadoLlamadaSimuladorDto<TRespuesta>> LlamarConReintentos<TRespuesta>(
        Func<Task<ResultadoLlamadaSimuladorDto<TRespuesta>>> llamada)
    {
        var resultado = await llamada();

        foreach (var espera in EsperasEntreReintentosSegundos)
        {
            if (resultado.Exitosa)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(espera));
            resultado = await llamada();
        }

        return resultado;
    }

    private static string ArmarIdCorrelacionSimulador(int idEnvio) =>
        $"MED-{RelojLima.Ahora():yyyy-MM-dd}-{idEnvio:D6}";

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
