using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConsultasPlan.Data;
using ConsultasPlan.Dtos;
using ConsultasPlan.Models;

namespace ConsultasPlan.Controllers;

[ApiController]
[Route("[controller]")]
public class AfiliadosController : ControllerBase
{
    private readonly AppDbContext _context;

    public AfiliadosController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var afiliados = await _context.Afiliados.Where(a => a.isActive).ToListAsync();
        return Ok(afiliados);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var afiliado = await _context.Afiliados.FindAsync(id);
        if (afiliado == null) return NotFound();
        return Ok(afiliado);
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
            createdAt = DateTime.Now,
            isActive = true
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
        afiliado.isActive = false;
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
