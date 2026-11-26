using Microsoft.EntityFrameworkCore;
using ConsultasPlan.Data;
using ConsultasPlan.Exceptions;
using ConsultasPlan.Models;
using ConsultasPlan.Settings;
using Microsoft.Extensions.Options;

namespace ConsultasPlan.Services;

public record CupoNutricion(int Anio, int Limite, int Usadas, int Disponibles, IReadOnlyDictionary<int, int> UsadasPorMes);

/// <summary>
/// Política de cupo: 6 consultas presenciales de nutrición por año calendario, máximo 1 por mes.
/// El cupo se calcula siempre a partir de las consultas (no hay contador persistido), por lo que
/// no puede desincronizarse y las consultas no usadas no se acumulan al año siguiente.
/// </summary>
public class CupoNutricionService
{
    public const string Especialidad = "Nutrición";
    // Una consulta cancelada libera el cupo; una inasistencia lo consume.
    private static readonly EstadoConsulta[] EstadosQueConsumen =
    {
        EstadoConsulta.Pendiente, EstadoConsulta.Confirmado,
        EstadoConsulta.Atendido, EstadoConsulta.Inasistencia
    };

    private readonly AppDbContext _context;

    private readonly BusinessRulesSettings _rules;

    public CupoNutricionService(AppDbContext context, IOptions<BusinessRulesSettings> rules)
    {
        _context = context;
        _rules = rules.Value;
    }

    public int LimiteAnual => _rules.NutricionAnnualLimit;
    public int LimiteMensual => _rules.NutricionMonthlyLimit;

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
        var porMes = fechas.GroupBy(f => f.Month).ToDictionary(g => g.Key, g => g.Count());
        return new CupoNutricion(anio, LimiteAnual, fechas.Count, Math.Max(0, LimiteAnual - fechas.Count), porMes);
    }

    public async Task<Dictionary<int, int>> UsadasPorAfiliadoAsync(int anio) =>
        (await Consumidas(anio).Where(c => c.AfiliadoId != null)
            .GroupBy(c => c.AfiliadoId!.Value)
            .Select(g => new { g.Key, N = g.Count() }).ToListAsync())
        .ToDictionary(x => x.Key, x => x.N);

    /// <summary>Lanza BusinessException si agendar la consulta viola el cupo; no hace nada si es válida o no aplica.</summary>
    public async Task ValidarAsync(int afiliadoId, Profesional profesional, ModalidadConsulta modalidad, DateTime fecha)
    {
        if (profesional.Especialidad != Especialidad || modalidad != ModalidadConsulta.Presencial)
            return;

        var cupo = await ObtenerAsync(afiliadoId, fecha.Year);
        if (cupo.Usadas >= LimiteAnual)
            throw new BusinessException($"El afiliado alcanzó el cupo de {LimiteAnual} consultas presenciales de nutrición para {fecha.Year}.");
        if (cupo.UsadasPorMes.GetValueOrDefault(fecha.Month) >= LimiteMensual)
            throw new BusinessException($"El afiliado ya tiene una consulta presencial de nutrición en {fecha:MM/yyyy}; el máximo es {LimiteMensual} por mes.");
    }
}
