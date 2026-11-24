using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConsultasPlan.Data;
using ConsultasPlan.Dtos;
using ConsultasPlan.Models;
using ConsultasPlan.Services;

namespace ConsultasPlan.Controllers;

[ApiController]
[Route("[controller]")]
public class AfiliadosController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly CupoNutricionService _cupo;

    public AfiliadosController(AppDbContext context, CupoNutricionService cupo)
    {
        _context = context;
        _cupo = cupo;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var afiliados = await _context.Afiliados.Where(a => a.IsActive).ToListAsync();
        var usadas = await _cupo.UsadasPorAfiliadoAsync(DateTime.Now.Year);
        return Ok(afiliados.Select(a => new
        {
            a.Id, a.NombreCompleto, a.DNI, a.Email, a.Telefono, a.Plan,
            a.Bloqueado, a.FechaBloqueo, a.CreatedAt, a.IsActive,
            consultasNutricionUsadas = usadas.GetValueOrDefault(a.Id),
            consultasNutricionLimite = CupoNutricionService.LimiteAnual
        }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var afiliado = await _context.Afiliados.FindAsync(id);
        if (afiliado == null) return NotFound();
        return Ok(afiliado);
    }

    [HttpGet("{id}/cupo-nutricion")]
    public async Task<IActionResult> GetCupoNutricion(int id, [FromQuery] int? anio)
    {
        var afiliado = await _context.Afiliados.FindAsync(id);
        if (afiliado == null) return NotFound();
        return Ok(await _cupo.ObtenerAsync(id, anio ?? DateTime.Now.Year));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AfiliadoRequest request)
    {
        var error = await Validar(request, null);
        if (error != null) return error;

        // Solo se copian los campos editables: el cliente no puede fijar Bloqueado, Plan, etc.
        var afiliado = new Afiliado
        {
            NombreCompleto = request.NombreCompleto.Trim(),
            DNI = request.DNI.Trim(),
            Email = request.Email.Trim(),
            Telefono = request.Telefono.Trim(),
            CreatedAt = DateTime.Now,
            IsActive = true
        };
        _context.Afiliados.Add(afiliado);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = afiliado.Id }, afiliado);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] AfiliadoRequest request)
    {
        var existing = await _context.Afiliados.FindAsync(id);
        if (existing == null) return NotFound();

        var error = await Validar(request, id);
        if (error != null) return error;

        existing.NombreCompleto = request.NombreCompleto.Trim();
        existing.DNI = request.DNI.Trim();
        existing.Email = request.Email.Trim();
        existing.Telefono = request.Telefono.Trim();

        await _context.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var afiliado = await _context.Afiliados.FindAsync(id);
        if (afiliado == null) return NotFound();

        // Baja lógica: conserva el historial de consultas (antes quedaban huérfanas por SetNull).
        afiliado.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<IActionResult?> Validar(AfiliadoRequest r, int? idActual)
    {
        if (string.IsNullOrWhiteSpace(r.NombreCompleto) || string.IsNullOrWhiteSpace(r.DNI))
            return BadRequest(new { mensaje = "Nombre y DNI son obligatorios." });

        var dni = r.DNI.Trim();
        if (await _context.Afiliados.AnyAsync(a => a.DNI == dni && a.Id != idActual))
            return Conflict(new { mensaje = "Ya existe un afiliado con ese DNI." });
        return null;
    }
}
