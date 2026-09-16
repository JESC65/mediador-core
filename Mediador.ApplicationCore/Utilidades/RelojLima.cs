namespace Mediador.ApplicationCore.Utilidades;

public static class RelojLima
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(-5);

    public static DateTimeOffset Ahora()
    {
        var ahora = DateTimeOffset.UtcNow.ToOffset(Offset);
        return new DateTimeOffset(
            ahora.Year, ahora.Month, ahora.Day,
            ahora.Hour, ahora.Minute, ahora.Second,
            ahora.Offset);
    }
}
