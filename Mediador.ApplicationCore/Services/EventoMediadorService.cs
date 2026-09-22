using System.Text.Json;
using Mediador.ApplicationCore.Dtos;
using Mediador.ApplicationCore.Interfaces;

namespace Mediador.ApplicationCore.Services;

public class EventoMediadorService : IEventoMediadorService
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    // Orden fijo de los estados de un envío ya confirmado (nota del sábado).
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

    public EventoMediadorService(
        IEventoRepository eventoRepositorio,
        IEnvioRepository envioRepositorio,
        IRutaTarifaEventoRepository rutaTarifaRepositorio)
    {
        _eventoRepositorio = eventoRepositorio;
        _envioRepositorio = envioRepositorio;
        _rutaTarifaRepositorio = rutaTarifaRepositorio;
    }

    public async Task<ResultadoEventoDto> ProcesarEvento(EventoRequestDto request)
    {
        // Paso 1 — Si el idEvento ya llegó antes, se responde igual pero no se procesa de nuevo.
        if (await _eventoRepositorio.ExisteEvento(request.IdEvento))
        {
            return ResultadoEventoDto.Ok(new EventoResponseDto { IdEvento = request.IdEvento, Recibido = true });
        }

        var esEventoDeCatalogo = TiposDeCatalogo.Contains(request.TipoEvento);

        // Los eventos de catálogo (tarifa, ruta) no traen codigoSeguimiento —
        // no están atados a un envío puntual.
        EnvioEventoDto? envio = null;
        if (!esEventoDeCatalogo)
        {
            var codigoSeguimiento = LeerTexto(request.Datos, "codigoSeguimiento");
            if (codigoSeguimiento is not null)
            {
                envio = await _envioRepositorio.BuscarPorCodigoSeguimiento(codigoSeguimiento);
            }
        }

        // Paso 2 — Guardar el evento siempre, con Procesado = 0.
        var idEventoRecibido = await _eventoRepositorio.GuardarEvento(new EventoParaGuardarDto
        {
            IdEvento = request.IdEvento,
            TipoEvento = request.TipoEvento,
            IdTransportista = request.IdTransportista,
            IdEnvio = envio?.IdEnvio,
            FechaEvento = request.FechaEvento,
            Contenido = JsonSerializer.Serialize(request, OpcionesJson)
        });

        // Evento huérfano: no es de catálogo y no se encontró el envío por su código.
        // No es un error — queda registrado para revisión.
        if (!esEventoDeCatalogo && envio is null)
        {
            await _eventoRepositorio.MarcarComoProcesado(idEventoRecibido);
            return ResultadoEventoDto.Ok(new EventoResponseDto { IdEvento = request.IdEvento, Recibido = true });
        }

        // Paso 3 — Procesar según el tipo.
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
                if (EstadoSegunTipoEvento.TryGetValue(request.TipoEvento, out var nuevoEstado)
                    && envio!.Estado != nuevoEstado)
                {
                    if (!EsTransicionValida(envio.Estado, nuevoEstado))
                    {
                        // El evento ya quedó guardado con Procesado = 0: se registró,
                        // pero la transición se rechaza y no se aplica.
                        return ResultadoEventoDto.Conflicto(envio.Estado);
                    }

                    var comentario = ArmarComentario(request.TipoEvento, request.Datos);
                    await _envioRepositorio.ActualizarEstado(envio.IdEnvio, nuevoEstado, "EETT", comentario);
                }
                break;
        }

        // Paso 4 — Marcar como procesado.
        await _eventoRepositorio.MarcarComoProcesado(idEventoRecibido);
        return ResultadoEventoDto.Ok(new EventoResponseDto { IdEvento = request.IdEvento, Recibido = true });
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
