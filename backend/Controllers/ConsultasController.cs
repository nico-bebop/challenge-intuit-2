using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConsultasPlan.Data;
using ConsultasPlan.Dtos;
using ConsultasPlan.Exceptions;
using ConsultasPlan.Models;
using ConsultasPlan.Services;
using ConsultasPlan.Settings;
using Microsoft.Extensions.Options;

namespace ConsultasPlan.Controllers;

[ApiController]
[Route("[controller]")]
public class ConsultasController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly CupoNutricionService _cupo;
    private readonly BusinessRulesSettings _rules;

    public ConsultasController(AppDbContext context, CupoNutricionService cupo, IOptions<BusinessRulesSettings> rules)
    {
        _context = context;
        _cupo = cupo;
        _rules = rules.Value;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var consultas = await _context.Consultas
            .Include(c => c.Afiliado)
            .Include(c => c.Profesional)
            .ToListAsync();
        return Ok(consultas);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var consulta = await _context.Consultas
            .Include(c => c.Afiliado)
            .Include(c => c.Profesional)
            .FirstOrDefaultAsync(c => c.Id == id);
        return Ok(consulta ?? throw new NotFoundException("Consulta no encontrada."));
    }

    [HttpPost]
    public async Task<IActionResult> CrearConsulta([FromBody] CrearConsultaRequest request)
    {
        var afiliado = request.AfiliadoId == null ? null : await _context.Afiliados.FindAsync(request.AfiliadoId);
        if (afiliado == null || !afiliado.IsActive)
            throw new NotFoundException("Afiliado no encontrado.");

        if (afiliado.Bloqueado)
            throw new BusinessException("El afiliado se encuentra bloqueado para agendar consultas.");

        if (request.FechaHora <= DateTime.Now)
            throw new BusinessException("La fecha de la consulta debe ser futura.");

        if (!Enum.IsDefined(request.Modalidad))
            throw new BusinessException("Modalidad inválida.");

        var profesional = await _context.Profesionales.FindAsync(request.ProfesionalId)
            ?? throw new NotFoundException("Profesional no encontrado.");

        await _cupo.ValidarAsync(afiliado.Id, profesional, request.Modalidad, request.FechaHora);

        var consultaConflicto = await _context.Consultas.AnyAsync(c =>
            c.ProfesionalId == request.ProfesionalId &&
            c.FechaHora == request.FechaHora &&
            c.Estado != EstadoConsulta.Cancelado);
        if (consultaConflicto)
            throw new BusinessException("El profesional ya tiene una consulta en ese horario.");

        var consulta = new Consulta
        {
            AfiliadoId = afiliado.Id,
            ProfesionalId = request.ProfesionalId,
            FechaHora = request.FechaHora,
            Modalidad = request.Modalidad,
            Motivo = request.Motivo?.Trim() ?? string.Empty,
            FechaCreacion = DateTime.Now,
            Estado = EstadoConsulta.Pendiente
        };
        _context.Consultas.Add(consulta);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = consulta.Id }, consulta);
    }

    // POST (antes GET): una operación que modifica estado no debe ser "safe"/cacheable/prefetcheable.
    [HttpPost("{id}/cancelar")]
    public async Task<IActionResult> CancelarConsulta(int id) =>
        await Modificar(id, c => c.Cancelar(DateTime.Now, _rules.CancellationWindowHours));

    [HttpPost("{id}/inasistencia")]
    public async Task<IActionResult> MarcarInasistencia(int id) =>
        await Modificar(id, c => c.MarcarInasistencia(DateTime.Now));

    [HttpPost("{id}/atencion")]
    public async Task<IActionResult> RegistrarAtencion(int id) =>
        await Modificar(id, c => c.RegistrarAtencion(DateTime.Now));

    [HttpPut("{id}/estado")]
    public async Task<IActionResult> ActualizarEstado(int id, [FromBody] ActualizarEstadoRequest request) =>
        await Modificar(id, c => c.CambiarEstado(request.Estado, DateTime.Now, _rules.CancellationWindowHours));

    private async Task<IActionResult> Modificar(int id, Action<Consulta> accion)
    {
        var consulta = await _context.Consultas.FindAsync(id)
            ?? throw new NotFoundException("Consulta no encontrada.");
        accion(consulta);
        await _context.SaveChangesAsync();
        return Ok(consulta);
    }
}

public class ActualizarEstadoRequest
{
    public EstadoConsulta Estado { get; set; }
}
