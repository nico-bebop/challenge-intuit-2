using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ConsultasPlan.Data;
using ConsultasPlan.Dtos;
using ConsultasPlan.Exceptions;
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
            consultasNutricionLimite = _cupo.LimiteAnual
        }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id) => Ok(await Obtener(id));

    [HttpGet("{id}/cupo-nutricion")]
    public async Task<IActionResult> GetCupoNutricion(int id, [FromQuery] int? anio)
    {
        await Obtener(id);
        return Ok(await _cupo.ObtenerAsync(id, anio ?? DateTime.Now.Year));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AfiliadoRequest request)
    {
        await Validar(request, null);

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
        var existing = await Obtener(id);
        await Validar(request, id);

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
        var afiliado = await Obtener(id);
        // Baja lógica: conserva el historial de consultas (antes quedaban huérfanas por SetNull).
        afiliado.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<Afiliado> Obtener(int id) =>
        await _context.Afiliados.FindAsync(id) ?? throw new NotFoundException("Afiliado no encontrado.");

    private async Task Validar(AfiliadoRequest r, int? idActual)
    {
        if (string.IsNullOrWhiteSpace(r.NombreCompleto) || string.IsNullOrWhiteSpace(r.DNI))
            throw new BusinessException("Nombre y DNI son obligatorios.");

        var dni = r.DNI.Trim();
        if (await _context.Afiliados.AnyAsync(a => a.DNI == dni && a.Id != idActual))
            throw new ConflictException("Ya existe un afiliado con ese DNI.");
    }
}
