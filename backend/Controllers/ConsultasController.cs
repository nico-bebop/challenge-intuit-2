using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConsultasPlan.Data;
using ConsultasPlan.Dtos;
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
        if (consulta == null) return NotFound();
        return Ok(consulta);
    }

    [HttpPost]
    public async Task<IActionResult> CrearConsulta([FromBody] CrearConsultaRequest request)
    {
        var afiliado = request.AfiliadoId == null ? null : await _context.Afiliados.FindAsync(request.AfiliadoId);
        if (afiliado == null || !afiliado.IsActive)
            return NotFound(new { mensaje = "Afiliado no encontrado." });

        if (afiliado.Bloqueado)
            return BadRequest(new { mensaje = "El afiliado se encuentra bloqueado para agendar consultas." });

        if (request.FechaHora <= DateTime.Now)
            return BadRequest(new { mensaje = "La fecha de la consulta debe ser futura." });

        if (!Enum.IsDefined(request.Modalidad))
            return BadRequest(new { mensaje = "Modalidad inválida." });

        var profesional = await _context.Profesionales.FindAsync(request.ProfesionalId);
        if (profesional == null)
            return NotFound(new { mensaje = "Profesional no encontrado." });

        var errorCupo = await _cupo.ValidarAsync(afiliado.Id, profesional, request.Modalidad, request.FechaHora);
        if (errorCupo != null)
            return BadRequest(new { mensaje = errorCupo });

        var consultaConflicto = await _context.Consultas.AnyAsync(c =>
            c.ProfesionalId == request.ProfesionalId &&
            c.FechaHora == request.FechaHora &&
            c.Estado != EstadoConsulta.Cancelado);
        if (consultaConflicto)
            return BadRequest(new { mensaje = "El profesional ya tiene una consulta en ese horario." });

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
    public async Task<IActionResult> CancelarConsulta(int id)
    {
        var consulta = await _context.Consultas.FindAsync(id);
        if (consulta == null) return NotFound();

        if (consulta.Estado is not (EstadoConsulta.Pendiente or EstadoConsulta.Confirmado))
            return BadRequest(new { mensaje = $"No se puede cancelar una consulta en estado {consulta.Estado}." });

        // Antes se comparaba contra 23h mientras el mensaje decía 24h.
        if (consulta.FechaHora - DateTime.Now < TimeSpan.FromHours(_rules.CancellationWindowHours))
            return BadRequest(new { mensaje = $"No se puede cancelar con menos de {_rules.CancellationWindowHours} horas de anticipación." });

        consulta.Estado = EstadoConsulta.Cancelado;
        await _context.SaveChangesAsync();
        return Ok(consulta);
    }

    [HttpPost("{id}/inasistencia")]
    public async Task<IActionResult> MarcarInasistencia(int id)
    {
        var consulta = await _context.Consultas.FindAsync(id);
        if (consulta == null) return NotFound();

        if (consulta.Estado is not (EstadoConsulta.Pendiente or EstadoConsulta.Confirmado))
            return BadRequest(new { mensaje = $"No se puede registrar una inasistencia sobre una consulta {consulta.Estado}." });

        if (consulta.FechaHora > DateTime.Now)
            return BadRequest(new { mensaje = "No se puede registrar una inasistencia sobre una consulta futura." });

        consulta.Estado = EstadoConsulta.Inasistencia;
        await _context.SaveChangesAsync();
        return Ok(consulta);
    }

    [HttpPost("{id}/atencion")]
    public async Task<IActionResult> RegistrarAtencion(int id)
    {
        var consulta = await _context.Consultas.FindAsync(id);
        if (consulta == null) return NotFound();

        if (consulta.Estado is not (EstadoConsulta.Pendiente or EstadoConsulta.Confirmado))
            return BadRequest(new { mensaje = $"No se puede registrar la atención de una consulta {consulta.Estado}." });

        // Antes se validaba "año en curso": permitía atender consultas futuras y bloqueaba las del año anterior.
        if (consulta.FechaHora > DateTime.Now)
            return BadRequest(new { mensaje = "No se puede registrar la atención de una consulta futura." });

        consulta.Estado = EstadoConsulta.Atendido;
        await _context.SaveChangesAsync();
        return Ok(consulta);
    }

    [HttpPut("{id}/estado")]
    public async Task<IActionResult> ActualizarEstado(int id, [FromBody] ActualizarEstadoRequest request)
    {
        var consulta = await _context.Consultas.FindAsync(id);
        if (consulta == null) return NotFound();

        // Solo transiciones válidas: antes se podía reabrir una cancelada o pisar cualquier estado.
        if (!EstadoConsultaTransiciones.Permitida(consulta.Estado, request.Estado))
            return BadRequest(new { mensaje = $"Transición de estado no permitida: {consulta.Estado} → {request.Estado}." });

        if (request.Estado == EstadoConsulta.Cancelado && consulta.FechaHora - DateTime.Now < TimeSpan.FromHours(_rules.CancellationWindowHours))
            return BadRequest(new { mensaje = $"No se puede cancelar con menos de {_rules.CancellationWindowHours} horas de anticipación." });

        if (request.Estado is EstadoConsulta.Atendido or EstadoConsulta.Inasistencia && consulta.FechaHora > DateTime.Now)
            return BadRequest(new { mensaje = "No se puede registrar atención/inasistencia sobre una consulta futura." });

        consulta.Estado = request.Estado;
        await _context.SaveChangesAsync();
        return Ok(consulta);
    }
}

public class ActualizarEstadoRequest
{
    public EstadoConsulta Estado { get; set; }
}
