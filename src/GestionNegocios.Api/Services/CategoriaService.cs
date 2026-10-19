using GestionNegocios.Api.Data;
using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionNegocios.Api.Services;

public class CategoriaService : ICategoriaService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CategoriaService> _logger;

    public CategoriaService(ApplicationDbContext context, ILogger<CategoriaService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResultDto<CategoriaDto>> GetPagedAsync(int page = 1, int pageSize = 10, string? search = null, bool? soloActivas = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Categorias.AsNoTracking().AsQueryable();

        if (soloActivas.HasValue)
        {
            query = query.Where(c => c.Activa == soloActivas.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c => c.Nombre.ToLower().Contains(term) ||
                                     (c.Descripcion != null && c.Descripcion.ToLower().Contains(term)));
        }

        var totalItems = await query.CountAsync();

        var items = await query
            .OrderBy(c => c.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CategoriaDto
            {
                Id = c.Id,
                Nombre = c.Nombre,
                Descripcion = c.Descripcion,
                Activa = c.Activa,
                ProductosCount = c.Productos.Count
            })
            .ToListAsync();

        return new PagedResultDto<CategoriaDto>(items, page, pageSize, totalItems);
    }

    public async Task<List<CategoriaDto>> GetAllAsync(bool soloActivas = true)
    {
        var query = _context.Categorias.AsNoTracking().AsQueryable();

        if (soloActivas)
        {
            query = query.Where(c => c.Activa);
        }

        return await query
            .OrderBy(c => c.Nombre)
            .Select(c => new CategoriaDto
            {
                Id = c.Id,
                Nombre = c.Nombre,
                Descripcion = c.Descripcion,
                Activa = c.Activa,
                ProductosCount = c.Productos.Count
            })
            .ToListAsync();
    }

    public async Task<CategoriaDto?> GetByIdAsync(int id)
    {
        var categoria = await _context.Categorias
            .AsNoTracking()
            .Include(c => c.Productos)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (categoria == null) return null;

        return new CategoriaDto
        {
            Id = categoria.Id,
            Nombre = categoria.Nombre,
            Descripcion = categoria.Descripcion,
            Activa = categoria.Activa,
            ProductosCount = categoria.Productos.Count
        };
    }

    public async Task<(CategoriaDto? result, bool duplicateName)> CreateAsync(CategoriaCreateDto dto)
    {
        var nombreTrim = dto.Nombre.Trim();

        // Validar unicidad del nombre
        var exists = await _context.Categorias
            .AnyAsync(c => c.Nombre.ToLower() == nombreTrim.ToLower());

        if (exists)
        {
            return (null, true);
        }

        var categoria = new Categoria
        {
            Nombre = nombreTrim,
            Descripcion = dto.Descripcion?.Trim(),
            Activa = true
        };

        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Categoría creada con éxito: {Nombre} (ID: {Id})", categoria.Nombre, categoria.Id);

        return (new CategoriaDto
        {
            Id = categoria.Id,
            Nombre = categoria.Nombre,
            Descripcion = categoria.Descripcion,
            Activa = categoria.Activa,
            ProductosCount = 0
        }, false);
    }

    public async Task<(CategoriaDto? result, bool duplicateName, bool notFound)> UpdateAsync(int id, CategoriaUpdateDto dto)
    {
        var categoria = await _context.Categorias
            .Include(c => c.Productos)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (categoria == null)
        {
            return (null, false, true);
        }

        var nombreTrim = dto.Nombre.Trim();

        // Validar unicidad excluyendo la propia categoría
        var duplicate = await _context.Categorias
            .AnyAsync(c => c.Id != id && c.Nombre.ToLower() == nombreTrim.ToLower());

        if (duplicate)
        {
            return (null, true, false);
        }

        categoria.Nombre = nombreTrim;
        categoria.Descripcion = dto.Descripcion?.Trim();
        categoria.Activa = dto.Activa;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Categoría actualizada con éxito: ID {Id}", id);

        return (new CategoriaDto
        {
            Id = categoria.Id,
            Nombre = categoria.Nombre,
            Descripcion = categoria.Descripcion,
            Activa = categoria.Activa,
            ProductosCount = categoria.Productos.Count
        }, false, false);
    }

    public async Task<(bool success, bool deactivated, string? message)> DeleteAsync(int id)
    {
        var categoria = await _context.Categorias
            .Include(c => c.Productos)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (categoria == null)
        {
            return (false, false, "La categoría no existe.");
        }

        if (categoria.Productos.Any())
        {
            categoria.Activa = false;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Categoría ID {Id} desactivada debido a que posee productos asociados.", id);
            return (true, true, "La categoría posee productos asociados. Ha sido desactivada para preservar la integridad.");
        }

        _context.Categorias.Remove(categoria);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Categoría ID {Id} eliminada físicamente con éxito.", id);
        return (true, false, "Categoría eliminada con éxito.");
    }
}
