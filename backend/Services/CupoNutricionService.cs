using Microsoft.EntityFrameworkCore;
using ConsultasPlan.Data;
using ConsultasPlan.Models;

namespace ConsultasPlan.Services;

public record CupoNutricion(int Anio, int Limite, int Usadas, int Disponibles, IReadOnlyList<int> MesesUsados);

/// <summary>
/// Política de cupo: 6 consultas presenciales de nutrición por año calendario, máximo 1 por mes.
/// El cupo se calcula siempre a partir de las consultas (no hay contador persistido), por lo que
/// no puede desincronizarse y las consultas no usadas no se acumulan al año siguiente.
/// </summary>
public class CupoNutricionService
{
    public const string Especialidad = "Nutrición";
    public const int LimiteAnual = 6;
    public const int LimiteMensual = 1;

    // Una consulta cancelada libera el cupo; una inasistencia lo consume.
    private static readonly EstadoConsulta[] EstadosQueConsumen =
    {
        EstadoConsulta.Pendiente, EstadoConsulta.Confirmado,
        EstadoConsulta.Atendido, EstadoConsulta.Inasistencia
    };

    private readonly AppDbContext _context;

    public CupoNutricionService(AppDbContext context) => _context = context;

    private IQueryable<Consulta> Consumidas(int anio)
    {
        var desde = new DateTime(anio, 1, 1);
        var hasta = desde.AddYears(1);
        return _context.Consultas.Where(c =>
            c.Profesional!.Especialidad == Especialidad &&
            c.Modalidad == ModalidadConsulta.Presencial &&
            EstadosQueConsumen.Contains(c.Estado) &&
            c.FechaHora >= desde && c.FechaHora < hasta);
    }

    public async Task<CupoNutricion> ObtenerAsync(int afiliadoId, int anio)
    {
        var fechas = await Consumidas(anio).Where(c => c.AfiliadoId == afiliadoId)
            .Select(c => c.FechaHora).ToListAsync();
        var meses = fechas.Select(f => f.Month).Distinct().OrderBy(m => m).ToList();
        return new CupoNutricion(anio, LimiteAnual, fechas.Count, Math.Max(0, LimiteAnual - fechas.Count), meses);
    }

    public async Task<Dictionary<int, int>> UsadasPorAfiliadoAsync(int anio) =>
        (await Consumidas(anio).Where(c => c.AfiliadoId != null)
            .GroupBy(c => c.AfiliadoId!.Value)
            .Select(g => new { g.Key, N = g.Count() }).ToListAsync())
        .ToDictionary(x => x.Key, x => x.N);

    /// <summary>Devuelve el mensaje de error si agendar la consulta viola el cupo; null si es válida o no aplica.</summary>
    public async Task<string?> ValidarAsync(int afiliadoId, Profesional profesional, ModalidadConsulta modalidad, DateTime fecha)
    {
        if (profesional.Especialidad != Especialidad || modalidad != ModalidadConsulta.Presencial)
            return null;

        var cupo = await ObtenerAsync(afiliadoId, fecha.Year);
        if (cupo.Usadas >= LimiteAnual)
            return $"El afiliado alcanzó el cupo de {LimiteAnual} consultas presenciales de nutrición para {fecha.Year}.";
        if (cupo.MesesUsados.Contains(fecha.Month))
            return $"El afiliado ya tiene una consulta presencial de nutrición en {fecha:MM/yyyy}; el máximo es {LimiteMensual} por mes.";
        return null;
    }
}
