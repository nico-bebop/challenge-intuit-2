using ConsultasPlan.Exceptions;

namespace ConsultasPlan.Models;

public class Consulta
{
    public int Id { get; set; }
    public int? AfiliadoId { get; set; }
    public Afiliado? Afiliado { get; set; }
    public int ProfesionalId { get; set; }
    public Profesional? Profesional { get; set; }
    public DateTime FechaHora { get; set; }
    public EstadoConsulta Estado { get; set; }
    public ModalidadConsulta Modalidad { get; set; }
    public DateTime FechaCreacion { get; set; }
    public string Motivo { get; set; } = string.Empty;

    // La consulta es quien conoce qué se puede hacer con ella: ninguna vía de entrada
    // (endpoint específico o PUT /estado) puede dejarla en un estado inválido.

    public void Cancelar(DateTime ahora, int ventanaCancelacionHoras) =>
        CambiarEstado(EstadoConsulta.Cancelado, ahora, ventanaCancelacionHoras);

    public void RegistrarAtencion(DateTime ahora) =>
        CambiarEstado(EstadoConsulta.Atendido, ahora, 0);

    public void MarcarInasistencia(DateTime ahora) =>
        CambiarEstado(EstadoConsulta.Inasistencia, ahora, 0);

    public void CambiarEstado(EstadoConsulta nuevo, DateTime ahora, int ventanaCancelacionHoras)
    {
        if (!EstadoConsultaTransiciones.Permitida(Estado, nuevo))
            throw new BusinessException($"Transición de estado no permitida: {Estado} → {nuevo}.");

        if (nuevo == EstadoConsulta.Cancelado && FechaHora - ahora < TimeSpan.FromHours(ventanaCancelacionHoras))
            throw new BusinessException($"No se puede cancelar con menos de {ventanaCancelacionHoras} horas de anticipación.");

        // Antes RegistrarAtencion validaba "año en curso", lo que permitía atender consultas futuras.
        if (nuevo is EstadoConsulta.Atendido or EstadoConsulta.Inasistencia && FechaHora > ahora)
            throw new BusinessException("No se puede registrar atención o inasistencia sobre una consulta futura.");

        Estado = nuevo;
    }
}
