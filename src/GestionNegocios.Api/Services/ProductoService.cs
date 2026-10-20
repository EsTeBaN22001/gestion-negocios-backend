using GestionNegocios.Api.Data;
using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionNegocios.Api.Services;

public class ProductoService : IProductoService
{
    private readonly ApplicationDbContext _context;

    public ProductoService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDto<ProductoListDto>> GetPagedAsync(
        int page = 1,
        int pageSize = 10,
        string? search = null,
        int? categoriaId = null,
        bool? soloActivos = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Stocks)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                EF.Functions.Like(p.Nombre, $"%{term}%") ||
                EF.Functions.Like(p.Codigo, $"%{term}%") ||
                (p.Descripcion != null && EF.Functions.Like(p.Descripcion, $"%{term}%")));
        }

        if (categoriaId.HasValue && categoriaId.Value > 0)
        {
            query = query.Where(p => p.CategoriaId == categoriaId.Value);
        }

        if (soloActivos.HasValue)
        {
            query = query.Where(p => p.Activo == soloActivos.Value);
        }

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var items = await query
            .OrderBy(p => p.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductoListDto
            {
                Id = p.Id,
                Codigo = p.Codigo,
                Nombre = p.Nombre,
                Descripcion = p.Descripcion,
                Precio = p.Precio,
                Activo = p.Activo,
                CategoriaId = p.CategoriaId,
                CategoriaNombre = p.Categoria.Nombre,
                StockTotal = p.Stocks.Sum(s => s.Cantidad)
            })
            .ToListAsync();

        return new PagedResultDto<ProductoListDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    public async Task<List<ProductoListDto>> GetAllAsync(bool soloActivos = true)
    {
        var query = _context.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Stocks)
            .AsNoTracking()
            .AsQueryable();

        if (soloActivos)
        {
            query = query.Where(p => p.Activo);
        }

        return await query
            .OrderBy(p => p.Nombre)
            .Select(p => new ProductoListDto
            {
                Id = p.Id,
                Codigo = p.Codigo,
                Nombre = p.Nombre,
                Descripcion = p.Descripcion,
                Precio = p.Precio,
                Activo = p.Activo,
                CategoriaId = p.CategoriaId,
                CategoriaNombre = p.Categoria.Nombre,
                StockTotal = p.Stocks.Sum(s => s.Cantidad)
            })
            .ToListAsync();
    }

    public async Task<ProductoDetailDto?> GetByIdAsync(int id)
    {
        var producto = await _context.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Stocks)
                .ThenInclude(s => s.Sucursal)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (producto == null) return null;

        return new ProductoDetailDto
        {
            Id = producto.Id,
            Codigo = producto.Codigo,
            Nombre = producto.Nombre,
            Descripcion = producto.Descripcion,
            Precio = producto.Precio,
            Activo = producto.Activo,
            CategoriaId = producto.CategoriaId,
            CategoriaNombre = producto.Categoria.Nombre,
            Stocks = producto.Stocks.Select(s => new ProductoStockResumenDto
            {
                SucursalId = s.SucursalId,
                SucursalNombre = s.Sucursal.Nombre,
                Cantidad = s.Cantidad,
                StockMinimo = s.StockMinimo
            }).ToList()
        };
    }

    public async Task<(ProductoDetailDto? Created, string? Error)> CreateAsync(ProductoCreateDto dto)
    {
        var trimmedCodigo = dto.Codigo.Trim();
        var trimmedNombre = dto.Nombre.Trim();

        // Verificar unicidad de código
        var codigoExiste = await _context.Productos
            .AnyAsync(p => p.Codigo.ToLower() == trimmedCodigo.ToLower());
        if (codigoExiste)
        {
            return (null, $"Ya existe un producto registrado con el código '{trimmedCodigo}'.");
        }

        // Verificar categoría existente
        var categoria = await _context.Categorias.FindAsync(dto.CategoriaId);
        if (categoria == null)
        {
            return (null, $"La categoría especificada (ID: {dto.CategoriaId}) no existe.");
        }

        var producto = new Producto
        {
            Codigo = trimmedCodigo,
            Nombre = trimmedNombre,
            Descripcion = dto.Descripcion?.Trim(),
            Precio = dto.Precio,
            CategoriaId = dto.CategoriaId,
            Activo = true
        };

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();

        var detail = new ProductoDetailDto
        {
            Id = producto.Id,
            Codigo = producto.Codigo,
            Nombre = producto.Nombre,
            Descripcion = producto.Descripcion,
            Precio = producto.Precio,
            Activo = producto.Activo,
            CategoriaId = producto.CategoriaId,
            CategoriaNombre = categoria.Nombre,
            Stocks = new List<ProductoStockResumenDto>()
        };

        return (detail, null);
    }

    public async Task<(ProductoDetailDto? Updated, string? Error, bool NotFound)> UpdateAsync(int id, ProductoUpdateDto dto)
    {
        var producto = await _context.Productos
            .Include(p => p.Stocks)
                .ThenInclude(s => s.Sucursal)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (producto == null)
        {
            return (null, null, true);
        }

        var trimmedCodigo = dto.Codigo.Trim();
        var trimmedNombre = dto.Nombre.Trim();

        // Verificar unicidad de código excluyendo el actual
        var codigoExiste = await _context.Productos
            .AnyAsync(p => p.Id != id && p.Codigo.ToLower() == trimmedCodigo.ToLower());
        if (codigoExiste)
        {
            return (null, $"Ya existe otro producto registrado con el código '{trimmedCodigo}'.", false);
        }

        // Verificar categoría existente
        var categoria = await _context.Categorias.FindAsync(dto.CategoriaId);
        if (categoria == null)
        {
            return (null, $"La categoría especificada (ID: {dto.CategoriaId}) no existe.", false);
        }

        producto.Codigo = trimmedCodigo;
        producto.Nombre = trimmedNombre;
        producto.Descripcion = dto.Descripcion?.Trim();
        producto.Precio = dto.Precio;
        producto.CategoriaId = dto.CategoriaId;
        producto.Activo = dto.Activo;

        await _context.SaveChangesAsync();

        var detail = new ProductoDetailDto
        {
            Id = producto.Id,
            Codigo = producto.Codigo,
            Nombre = producto.Nombre,
            Descripcion = producto.Descripcion,
            Precio = producto.Precio,
            Activo = producto.Activo,
            CategoriaId = producto.CategoriaId,
            CategoriaNombre = categoria.Nombre,
            Stocks = producto.Stocks.Select(s => new ProductoStockResumenDto
            {
                SucursalId = s.SucursalId,
                SucursalNombre = s.Sucursal.Nombre,
                Cantidad = s.Cantidad,
                StockMinimo = s.StockMinimo
            }).ToList()
        };

        return (detail, null, false);
    }

    public async Task<(bool Success, string? Error, bool Deactivated, bool NotFound)> DeleteAsync(int id)
    {
        var producto = await _context.Productos
            .Include(p => p.Stocks)
            .Include(p => p.DetallesVenta)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (producto == null)
        {
            return (false, null, false, true);
        }

        // Si tiene ventas o registros de stock, aplicar baja lógica para proteger integridad
        var tieneVentas = producto.DetallesVenta.Any();
        var tieneStock = producto.Stocks.Any(s => s.Cantidad > 0);

        if (tieneVentas || tieneStock)
        {
            producto.Activo = false;
            await _context.SaveChangesAsync();
            return (true, null, true, false);
        }

        // Si solo tiene stocks en 0, podemos limpiarlos y eliminar el producto
        if (producto.Stocks.Any())
        {
            _context.Stocks.RemoveRange(producto.Stocks);
        }

        _context.Productos.Remove(producto);
        await _context.SaveChangesAsync();
        return (true, null, false, false);
    }
}
