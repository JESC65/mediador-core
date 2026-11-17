namespace Mediador.ApplicationCore.Dtos;

public class EventoParaGuardarDto
{
    public string IdEvento { get; set; } = string.Empty;
    public string TipoEvento { get; set; } = string.Empty;
    public int? Secuencia { get; set; }
    public string? VersionContrato { get; set; }
    public string? EntidadTipo { get; set; }
    public string? EntidadId { get; set; }
    public int? VersionEntidad { get; set; }
    public string? Emisor { get; set; }
    public int? IdTransportista { get; set; }
    public int? IdEnvio { get; set; }
    public DateTimeOffset FechaEvento { get; set; }
    public string? Contenido { get; set; }
    public string? HuellaCuerpo { get; set; }
    public bool RequiereMensajeRechazado { get; set; }
    public string? MotivoRechazo { get; set; }
    public bool EsConflicto { get; set; }
}
