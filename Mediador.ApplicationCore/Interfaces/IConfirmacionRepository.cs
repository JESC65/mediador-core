using Mediador.ApplicationCore.Dtos;

namespace Mediador.ApplicationCore.Interfaces;

public interface IConfirmacionRepository
{
    Task<CotizacionParaConfirmarDto?> BuscarPorIdOpcion(string idOpcion);

    /// <summary>La siguiente cotización no descartada más barata del mismo envío, distinta de la actual.</summary>
    Task<CotizacionParaConfirmarDto?> ObtenerSiguienteAlternativa(int idEnvio, string idOpcionActual);

    Task<IReadOnlyList<PaqueteConfirmarDto>> ObtenerPaquetes(int idEnvio);

    /// <summary>Guarda o actualiza remitente y consignado (paso 3). Upsert por (IdEnvio, Rol).</summary>
    Task GuardarPersonas(int idEnvio, PersonaConfirmarDto remitente, PersonaConfirmarDto consignado);

    Task MarcarDescartada(int idCotizacion, string motivo);

    /// <summary>Crea la cotización nueva cuando el precio real difiere del estimado (paso 4, PRECIO_CAMBIO).</summary>
    Task<string> CrearCotizacionPorCambioPrecio(CotizacionParaConfirmarDto original, decimal precioReal, string idOpcionNueva);

    /// <summary>Siguiente código correlativo del año actual, formato MED-AAAA-NNNNNN. No lo persiste todavía.</summary>
    Task<string> GenerarSiguienteCodigoSeguimiento();

    /// <summary>Pasos 6 y 7 en una sola transacción: guarda el código, marca la cotización elegida e inserta el estado CONFIRMADO.</summary>
    Task<bool> ConfirmarTransaccional(int idEnvio, int idCotizacion, int idTransportista, decimal precioFinal, string codigoSeguimiento, DateTimeOffset fechaConfirmacion);

    /// <summary>
    /// Red de seguridad: el registro en la transportista (paso 5) ya tuvo éxito pero la
    /// transacción del paso 7 falló. Nunca debe perderse el numeroGuiaEETT — se guarda
    /// aparte, fuera de la transacción fallida, en Observacion (no hay columna dedicada).
    /// </summary>
    Task GuardarPendienteConfirmacion(int idEnvio, string codigoSeguimiento, string? numeroGuiaEett);
}
