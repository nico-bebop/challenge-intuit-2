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
    public async Task<IActionResult> CancelarConsulta(int id) => await CambiarEstado(id, EstadoConsulta.Cancelado);

    [HttpPost("{id}/inasistencia")]
    public async Task<IActionResult> MarcarInasistencia(int id) => await CambiarEstado(id, EstadoConsulta.Inasistencia);

    [HttpPost("{id}/atencion")]
    public async Task<IActionResult> RegistrarAtencion(int id) => await CambiarEstado(id, EstadoConsulta.Atendido);

    [HttpPut("{id}/estado")]
    public async Task<IActionResult> ActualizarEstado(int id, [FromBody] ActualizarEstadoRequest request) =>
        await CambiarEstado(id, request.Estado);

    // Único punto de entrada para cambios de estado: ninguna vía puede saltear las reglas.
    private async Task<IActionResult> CambiarEstado(int id, EstadoConsulta nuevo)
    {
        var consulta = await _context.Consultas.FindAsync(id)
            ?? throw new NotFoundException("Consulta no encontrada.");

        if (!EstadoConsultaTransiciones.Permitida(consulta.Estado, nuevo))
            throw new BusinessException($"Transición de estado no permitida: {consulta.Estado} → {nuevo}.");

        if (nuevo == EstadoConsulta.Cancelado &&
            consulta.FechaHora - DateTime.Now < TimeSpan.FromHours(_rules.CancellationWindowHours))
            throw new BusinessException($"No se puede cancelar con menos de {_rules.CancellationWindowHours} horas de anticipación.");

        // Antes RegistrarAtencion validaba "año en curso", lo que permitía atender consultas futuras.
        if (nuevo is EstadoConsulta.Atendido or EstadoConsulta.Inasistencia && consulta.FechaHora > DateTime.Now)
            throw new BusinessException("No se puede registrar atención o inasistencia sobre una consulta futura.");

        consulta.Estado = nuevo;
        await _context.SaveChangesAsync();
        return Ok(consulta);
    }
}

public class ActualizarEstadoRequest
{
    public EstadoConsulta Estado { get; set; }
}
