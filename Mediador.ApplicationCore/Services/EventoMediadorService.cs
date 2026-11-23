using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Interfaces;

namespace Mediador.ApplicationCore.Services;

public class EventoMediadorService : IEventoMediadorService
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    // Orden fijo de los estados de un envío ya confirmado.
    // Las transiciones normales solo avanzan una posición a la vez; ANULADO
    // y RECHAZADO se permiten desde cualquier estado anterior a ENTREGADO.
    private static readonly List<string> OrdenEstados = new()
    {
        "CONFIRMADO", "ENTREGADO_A_TRANSPORTISTA", "EN_VIAJE", "ARRIBADO", "ENTREGADO"
    };

    private static readonly Dictionary<string, string> EstadoSegunTipoEvento = new()
    {
        ["BUS_ASIGNADO"] = "EN_VIAJE",
        ["EN_VIAJE"] = "EN_VIAJE",
        ["ARRIBADO"] = "ARRIBADO",
        ["ENTREGADO"] = "ENTREGADO",
        ["RECHAZADO"] = "RECHAZADO"
    };

    private static readonly HashSet<string> TiposDeCatalogo = new() { "PRECIO_ACTUALIZADO", "RUTA_ACTUALIZADA" };

    private readonly IEventoRepository _eventoRepositorio;
    private readonly IEnvioRepository _envioRepositorio;
    private readonly IRutaTarifaEventoRepository _rutaTarifaRepositorio;
    private readonly INotificadorHuecoSecuencia _notificadorHueco;

    public EventoMediadorService(
        IEventoRepository eventoRepositorio,
        IEnvioRepository envioRepositorio,
        IRutaTarifaEventoRepository rutaTarifaRepositorio,
        INotificadorHuecoSecuencia notificadorHueco)
    {
        _eventoRepositorio = eventoRepositorio;
        _envioRepositorio = envioRepositorio;
        _rutaTarifaRepositorio = rutaTarifaRepositorio;
        _notificadorHueco = notificadorHueco;
    }

    public async Task<ResultadoEventoDto> ProcesarEvento(EventoRequestDto request)
    {
        var contenidoSerializado = JsonSerializer.Serialize(request, OpcionesJson);
        var huellaActual = CalcularHuella(contenidoSerializado);

        // Paso 1 — Idempotencia y conflictos (R5 / R6)
        var eventoExistente = await _eventoRepositorio.ObtenerEventoExistente(request.IdEvento);
        if (eventoExistente is not null)
        {
            var huellaExistente = eventoExistente.HuellaCuerpo;

            // Si no tenía huella persistida (retrocompatibilidad), se compara el contenido serializado
            var sonIguales = huellaExistente is not null
                ? string.Equals(huellaExistente, huellaActual, StringComparison.OrdinalIgnoreCase)
                : string.Equals(eventoExistente.Contenido, contenidoSerializado, StringComparison.Ordinal);

            if (sonIguales)
            {
                // Mismo idEvento con contenido exactamente igual: reacusar sin reprocesar
                return ResultadoEventoDto.Ok(new EventoResponseDto
                {
                    IdEvento = request.IdEvento,
                    Recibido = true
                });
            }

            // Mismo idEvento pero contenido diferente: registrar conflicto en BD y rechazar
            await _eventoRepositorio.GuardarEvento(new EventoParaGuardarDto
            {
                IdEvento = request.IdEvento,
                TipoEvento = request.TipoEvento,
                Secuencia = request.Secuencia,
                VersionContrato = request.VersionContrato,
                EntidadTipo = request.Entidad?.Tipo,
                EntidadId = request.Entidad?.Id,
                VersionEntidad = request.VersionEntidad,
                Emisor = request.Emisor,
                IdTransportista = request.IdTransportista,
                IdEnvio = null,
                FechaEvento = request.FechaEvento,
                Contenido = contenidoSerializado,
                HuellaCuerpo = huellaActual,
                RequiereMensajeRechazado = true,
                MotivoRechazo = "CONFLICTO_CONTENIDO",
                EsConflicto = true
            });

            return ResultadoEventoDto.ConflictoContenido(
                $"El idEvento '{request.IdEvento}' ya fue registrado previamente con un contenido diferente (R5).");
        }

        var esEventoDeCatalogo = TiposDeCatalogo.Contains(request.TipoEvento);

        // Paso 2 — Identificación del envío (prioridad Entidad.Id, fallback temporal)
        EnvioEventoDto? envio = null;
        if (!esEventoDeCatalogo)
        {
            string? codigoSeguimiento = null;

            if (!string.IsNullOrWhiteSpace(request.Entidad?.Id))
            {
                codigoSeguimiento = request.Entidad.Id;
            }
            else
            {
                // TODO: Retirar el fallback a request.Datos["codigoSeguimiento"] cuando el simulador antiguo se desactive por completo (C2 v2.0 §5).
                codigoSeguimiento = LeerTexto(request.Datos, "codigoSeguimiento");
            }

            if (codigoSeguimiento is not null)
            {
                envio = await _envioRepositorio.BuscarPorCodigoSeguimiento(codigoSeguimiento);
            }
        }

        // Paso 3 — Validación de transición de estado
        var esTransicionInvalida = false;
        string? motivoRechazo = null;
        string? nuevoEstado = null;

        if (!esEventoDeCatalogo && envio is not null)
        {
            if (EstadoSegunTipoEvento.TryGetValue(request.TipoEvento, out nuevoEstado)
                && envio.Estado != nuevoEstado)
            {
                if (!EsTransicionValida(envio.Estado, nuevoEstado))
                {
                    esTransicionInvalida = true;
                    motivoRechazo = $"TRANSICION_INVALIDA: No se permite pasar de '{envio.Estado}' a '{nuevoEstado}'.";
                }
            }
        }

        // Paso 4 — Guardar el evento en BD siempre antes de responder 202
        var idEventoRecibido = await _eventoRepositorio.GuardarEvento(new EventoParaGuardarDto
        {
            IdEvento = request.IdEvento,
            TipoEvento = request.TipoEvento,
            Secuencia = request.Secuencia,
            VersionContrato = request.VersionContrato,
            EntidadTipo = request.Entidad?.Tipo,
            EntidadId = request.Entidad?.Id,
            VersionEntidad = request.VersionEntidad,
            Emisor = request.Emisor,
            IdTransportista = request.IdTransportista,
            IdEnvio = envio?.IdEnvio,
            FechaEvento = request.FechaEvento,
            Contenido = contenidoSerializado,
            HuellaCuerpo = huellaActual,
            RequiereMensajeRechazado = esTransicionInvalida,
            MotivoRechazo = motivoRechazo,
            EsConflicto = false
        });

        // Paso 5 — Control de secuencias contiguas y huecos (R6)
        if (request.Secuencia.HasValue && request.IdTransportista > 0)
        {
            await ProcesarSecuencia(request.IdTransportista, request.Secuencia.Value);
        }

        // Si fue una transición inválida: el evento ya quedó guardado en BD marcado para
        // emitir MENSAJE_RECHAZADO (R6). No se aplica al envío y se retorna éxito (202).
        if (esTransicionInvalida)
        {
            return ResultadoEventoDto.Ok(new EventoResponseDto
            {
                IdEvento = request.IdEvento,
                Recibido = true
            });
        }

        // Evento huérfano: no es de catálogo y no se encontró el envío por su código.
        // No es un error — queda registrado para revisión.
        if (!esEventoDeCatalogo && envio is null)
        {
            await _eventoRepositorio.MarcarComoProcesado(idEventoRecibido);
            return ResultadoEventoDto.Ok(new EventoResponseDto
            {
                IdEvento = request.IdEvento,
                Recibido = true
            });
        }

        // Paso 6 — Aplicar efectos del evento
        switch (request.TipoEvento)
        {
            case "PRECIO_ACTUALIZADO":
                var precioDto = request.Datos.Deserialize<PrecioActualizadoDto>(OpcionesJson)!;
                await _rutaTarifaRepositorio.ActualizarOCrearTarifa(request.IdTransportista, precioDto);
                break;

            case "RUTA_ACTUALIZADA":
                var rutaDto = request.Datos.Deserialize<RutaEventoDto>(OpcionesJson)!;
                await _rutaTarifaRepositorio.ActualizarOCrearRuta(request.IdTransportista, rutaDto);
                break;

            default:
                if (nuevoEstado is not null && envio!.Estado != nuevoEstado)
                {
                    var comentario = ArmarComentario(request.TipoEvento, request.Datos);
                    await _envioRepositorio.ActualizarEstado(envio.IdEnvio, nuevoEstado, "EETT", comentario);
                }
                break;
        }

        // Paso 7 — Marcar como procesado
        await _eventoRepositorio.MarcarComoProcesado(idEventoRecibido);

        return ResultadoEventoDto.Ok(new EventoResponseDto
        {
            IdEvento = request.IdEvento,
            Recibido = true
        });
    }

    private async Task ProcesarSecuencia(int idTransportista, int secuencia)
    {
        var ultimaContigua = await _eventoRepositorio.ObtenerUltimaSecuenciaContigua(idTransportista);

        if (secuencia == ultimaContigua + 1)
        {
            // Secuencia contigua esperada: avanza la secuencia
            var nuevaContigua = secuencia;

            // Avanzar si ya teníamos secuencias posteriores consecutivas guardadas previamente
            var secuenciasPosteriores = await _eventoRepositorio.ObtenerSecuenciasGuardadasMayoresA(idTransportista, nuevaContigua);
            foreach (var secPost in secuenciasPosteriores)
            {
                if (secPost == nuevaContigua + 1)
                {
                    nuevaContigua = secPost;
                }
                else if (secPost > nuevaContigua + 1)
                {
                    break;
                }
            }

            await _eventoRepositorio.ActualizarUltimaSecuenciaContigua(idTransportista, nuevaContigua);
            await _eventoRepositorio.ResolverHuecosHasta(idTransportista, nuevaContigua);
        }
        else if (secuencia > ultimaContigua + 1)
        {
            // Salto detectado: registrar hueco para recuperación (R6)
            var secuenciaEsperada = ultimaContigua + 1;
            await _eventoRepositorio.RegistrarHueco(idTransportista, secuenciaEsperada, secuencia);
            
            // Notificar al worker de recuperación para despertar inmediatamente
            _notificadorHueco.NotificarHueco(idTransportista);
            // La secuencia contigua no avanza
        }
        else
        {
            // secuencia <= ultimaContigua (reintento o evento fuera de orden que cubre un hueco pasado)
            var nuevaContigua = ultimaContigua;
            var secuenciasPosteriores = await _eventoRepositorio.ObtenerSecuenciasGuardadasMayoresA(idTransportista, nuevaContigua);
            foreach (var secPost in secuenciasPosteriores)
            {
                if (secPost == nuevaContigua + 1)
                {
                    nuevaContigua = secPost;
                }
                else if (secPost > nuevaContigua + 1)
                {
                    break;
                }
            }

            if (nuevaContigua > ultimaContigua)
            {
                await _eventoRepositorio.ActualizarUltimaSecuenciaContigua(idTransportista, nuevaContigua);
                await _eventoRepositorio.ResolverHuecosHasta(idTransportista, nuevaContigua);
            }
        }
    }

    private static string CalcularHuella(string contenido)
    {
        var bytes = Encoding.UTF8.GetBytes(contenido);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static bool EsTransicionValida(string estadoActual, string nuevoEstado)
    {
        if (nuevoEstado is "ANULADO" or "RECHAZADO")
        {
            return estadoActual != "ENTREGADO" && OrdenEstados.Contains(estadoActual);
        }

        if (nuevoEstado == "EN_VIAJE")
        {
            return estadoActual is "CONFIRMADO" or "ENTREGADO_A_TRANSPORTISTA";
        }

        var indiceActual = OrdenEstados.IndexOf(estadoActual);
        var indiceNuevo = OrdenEstados.IndexOf(nuevoEstado);

        return indiceActual >= 0 && indiceNuevo == indiceActual + 1;
    }

    private static string? ArmarComentario(string tipoEvento, JsonElement datos) => tipoEvento switch
    {
        "BUS_ASIGNADO" => LeerTexto(datos, "placaBus") is { } placa ? $"Bus {placa}" : null,
        "RECHAZADO" => LeerTexto(datos, "motivo"),
        _ => null
    };

    private static string? LeerTexto(JsonElement datos, string propiedad) =>
        datos.ValueKind == JsonValueKind.Object && datos.TryGetProperty(propiedad, out var valor)
            ? valor.GetString()
            : null;
}
