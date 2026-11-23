namespace Mediador.ApplicationCore.Dtos;

public class EventoExistenteDto
{
    public int IdEventoRecibido { get; set; }
    public string IdEvento { get; set; } = string.Empty;
    public string? HuellaCuerpo { get; set; }
    public string? Contenido { get; set; }
    public bool Procesado { get; set; }
    public bool EsConflicto { get; set; }
    public bool RequiereMensajeRechazado { get; set; }
}
