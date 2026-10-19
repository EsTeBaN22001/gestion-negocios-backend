using GestionNegocios.Api.Data;
using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionNegocios.Api.Services;

public class SucursalService : ISucursalService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SucursalService> _logger;

    public SucursalService(ApplicationDbContext context, ILogger<SucursalService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResultDto<SucursalDto>> GetPagedAsync(int page = 1, int pageSize = 10, string? search = null, bool? soloActivas = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Sucursales.AsNoTracking().AsQueryable();

        if (soloActivas.HasValue)
        {
            query = query.Where(s => s.Activa == soloActivas.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(s => s.Nombre.ToLower().Contains(term) ||
                                     (s.Direccion != null && s.Direccion.ToLower().Contains(term)) ||
                                     (s.Telefono != null && s.Telefono.ToLower().Contains(term)));
        }

        var totalItems = await query.CountAsync();

        var items = await query
            .OrderBy(s => s.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SucursalDto
            {
                Id = s.Id,
                Nombre = s.Nombre,
                Direccion = s.Direccion,
                Telefono = s.Telefono,
                Activa = s.Activa,
                UsuariosCount = s.Usuarios.Count
            })
            .ToListAsync();

        return new PagedResultDto<SucursalDto>(items, page, pageSize, totalItems);
    }

    public async Task<List<SucursalDto>> GetAllAsync(bool soloActivas = true)
    {
        var query = _context.Sucursales.AsNoTracking().AsQueryable();

        if (soloActivas)
        {
            query = query.Where(s => s.Activa);
        }

        return await query
            .OrderBy(s => s.Nombre)
            .Select(s => new SucursalDto
            {
                Id = s.Id,
                Nombre = s.Nombre,
                Direccion = s.Direccion,
                Telefono = s.Telefono,
                Activa = s.Activa,
                UsuariosCount = s.Usuarios.Count
            })
            .ToListAsync();
    }

    public async Task<SucursalDto?> GetByIdAsync(int id)
    {
        var sucursal = await _context.Sucursales
            .AsNoTracking()
            .Include(s => s.Usuarios)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sucursal == null) return null;

        return new SucursalDto
        {
            Id = sucursal.Id,
            Nombre = sucursal.Nombre,
            Direccion = sucursal.Direccion,
            Telefono = sucursal.Telefono,
            Activa = sucursal.Activa,
            UsuariosCount = sucursal.Usuarios.Count
        };
    }

    public async Task<SucursalDto> CreateAsync(SucursalCreateDto dto)
    {
        var sucursal = new Sucursal
        {
            Nombre = dto.Nombre.Trim(),
            Direccion = dto.Direccion?.Trim(),
            Telefono = dto.Telefono?.Trim(),
            Activa = true
        };

        _context.Sucursales.Add(sucursal);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Sucursal creada con éxito: {Nombre} (ID: {Id})", sucursal.Nombre, sucursal.Id);

        return new SucursalDto
        {
            Id = sucursal.Id,
            Nombre = sucursal.Nombre,
            Direccion = sucursal.Direccion,
            Telefono = sucursal.Telefono,
            Activa = sucursal.Activa,
            UsuariosCount = 0
        };
    }

    public async Task<SucursalDto?> UpdateAsync(int id, SucursalUpdateDto dto)
    {
        var sucursal = await _context.Sucursales
            .Include(s => s.Usuarios)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sucursal == null) return null;

        sucursal.Nombre = dto.Nombre.Trim();
        sucursal.Direccion = dto.Direccion?.Trim();
        sucursal.Telefono = dto.Telefono?.Trim();
        sucursal.Activa = dto.Activa;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Sucursal actualizada con éxito: ID {Id}", id);

        return new SucursalDto
        {
            Id = sucursal.Id,
            Nombre = sucursal.Nombre,
            Direccion = sucursal.Direccion,
            Telefono = sucursal.Telefono,
            Activa = sucursal.Activa,
            UsuariosCount = sucursal.Usuarios.Count
        };
    }

    public async Task<(bool success, bool deactivated, string? message)> DeleteAsync(int id)
    {
        var sucursal = await _context.Sucursales
            .Include(s => s.Usuarios)
            .Include(s => s.Stocks)
            .Include(s => s.Ventas)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sucursal == null)
        {
            return (false, false, "La sucursal no existe.");
        }

        // Si tiene datos asociados, no se puede eliminar físicamente por integridad referencial
        var tieneDependencias = sucursal.Usuarios.Any() || sucursal.Stocks.Any() || sucursal.Ventas.Any();

        if (tieneDependencias)
        {
            sucursal.Activa = false;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Sucursal ID {Id} desactivada debido a que posee entidades asociadas.", id);
            return (true, true, "La sucursal posee usuarios, stock o ventas asociadas. Ha sido desactivada para preservar la integridad.");
        }

        _context.Sucursales.Remove(sucursal);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Sucursal ID {Id} eliminada físicamente con éxito.", id);
        return (true, false, "Sucursal eliminada con éxito.");
    }
}
