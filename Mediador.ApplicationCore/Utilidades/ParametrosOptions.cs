using Microsoft.Extensions.Configuration;

namespace Mediador.ApplicationCore.Utilidades;

public class ParametrosOptions
{
    public const string Seccion = "Parametros";

    private static readonly TimeSpan[] TiemposPorDefecto =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10)
    ];

    private TimeSpan[]? _esperas;

    [ConfigurationKeyName("t_precio_eett")]
    public TimeSpan t_precio_eett { get; set; } = TimeSpan.FromSeconds(15);

    [ConfigurationKeyName("t_registro_eett")]
    public TimeSpan t_registro_eett { get; set; } = TimeSpan.FromSeconds(30);

    [ConfigurationKeyName("r_esperas_sincronas")]
    public TimeSpan[] r_esperas_sincronas
    {
        get => _esperas ?? TiemposPorDefecto;
        set
        {
            if (_esperas is null && value is not null && value.Length > TiemposPorDefecto.Length &&
                value.Take(TiemposPorDefecto.Length).SequenceEqual(TiemposPorDefecto))
            {
                _esperas = value.Skip(TiemposPorDefecto.Length).ToArray();
            }
            else
            {
                _esperas = value;
            }
        }
    }

    [ConfigurationKeyName("t_vigencia_cotizacion")]
    public TimeSpan t_vigencia_cotizacion { get; set; } = TimeSpan.FromHours(2);
}

