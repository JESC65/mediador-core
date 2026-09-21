namespace Mediador.ApplicationCore.Dtos.Simulador;

/// <summary>
/// Debe serializar EXACTAMENTE como el "paquetes" que espera POST /cotizar
/// del simulador (ver PaqueteCotizacionDto en SimuladorEETT).
/// </summary>
public class SimuladorPaqueteCotizarDto
{
    public decimal PesoKg { get; set; }
    public decimal LargoCm { get; set; }
    public decimal AnchoCm { get; set; }
    public decimal AltoCm { get; set; }
    public bool EsFragil { get; set; }
}
