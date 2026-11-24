namespace ConsultasPlan.Models;

public static class EstadoConsultaTransiciones
{
    // Cancelado, Atendido e Inasistencia son estados finales.
    public static bool Permitida(EstadoConsulta desde, EstadoConsulta hacia) => (desde, hacia) switch
    {
        (EstadoConsulta.Pendiente, EstadoConsulta.Confirmado) => true,
        (EstadoConsulta.Pendiente, EstadoConsulta.Cancelado) => true,
        (EstadoConsulta.Pendiente, EstadoConsulta.Atendido) => true,
        (EstadoConsulta.Pendiente, EstadoConsulta.Inasistencia) => true,
        (EstadoConsulta.Confirmado, EstadoConsulta.Cancelado) => true,
        (EstadoConsulta.Confirmado, EstadoConsulta.Atendido) => true,
        (EstadoConsulta.Confirmado, EstadoConsulta.Inasistencia) => true,
        _ => false
    };
}
