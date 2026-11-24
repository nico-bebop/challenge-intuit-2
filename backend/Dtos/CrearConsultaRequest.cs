using ConsultasPlan.Models;

namespace ConsultasPlan.Dtos;

public class CrearConsultaRequest
{
    public int? AfiliadoId { get; set; }
    public int ProfesionalId { get; set; }
    public DateTime FechaHora { get; set; }
    public ModalidadConsulta Modalidad { get; set; }
    public string Motivo { get; set; } = string.Empty;
}
