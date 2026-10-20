using GestionNegocios.Api.Data;
using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionNegocios.Api.Services;

public class StockService : IStockService
{
    private readonly ApplicationDbContext _context;

    public StockService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDto<StockListDto>> GetPagedAsync(
        int page = 1,
        int pageSize = 10,
        int? sucursalId = null,
        string? search = null,
        bool? soloBajoStock = null,
        int? userSucursalId = null,
        bool isAdmin = false)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Stocks
            .Include(s => s.Producto)
                .ThenInclude(p => p.Categoria)
            .Include(s => s.Sucursal)
            .AsNoTracking()
            .AsQueryable();

        // Si no es Administrador, forzar la sucursal del empleado
        if (!isAdmin)
        {
            if (!userSucursalId.HasValue)
            {
                // Empleado sin sucursal asignada: no tiene acceso a stock
                return new PagedResultDto<StockListDto>
                {
                    Items = new List<StockListDto>(),
                    Page = page,
                    PageSize = pageSize,
                    TotalItems = 0,
                    TotalPages = 0
                };
            }

            query = query.Where(s => s.SucursalId == userSucursalId.Value);
        }
        else if (sucursalId.HasValue && sucursalId.Value > 0)
        {
            query = query.Where(s => s.SucursalId == sucursalId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s =>
                EF.Functions.Like(s.Producto.Nombre, $"%{term}%") ||
                EF.Functions.Like(s.Producto.Codigo, $"%{term}%") ||
                EF.Functions.Like(s.Sucursal.Nombre, $"%{term}%"));
        }

        if (soloBajoStock.HasValue && soloBajoStock.Value)
        {
            query = query.Where(s => s.Cantidad <= s.StockMinimo);
        }

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var items = await query
            .OrderBy(s => s.Sucursal.Nombre)
            .ThenBy(s => s.Producto.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StockListDto
            {
                Id = s.Id,
                ProductoId = s.ProductoId,
                ProductoCodigo = s.Producto.Codigo,
                ProductoNombre = s.Producto.Nombre,
                ProductoPrecio = s.Producto.Precio,
                CategoriaNombre = s.Producto.Categoria.Nombre,
                SucursalId = s.SucursalId,
                SucursalNombre = s.Sucursal.Nombre,
                Cantidad = s.Cantidad,
                StockMinimo = s.StockMinimo
            })
            .ToListAsync();

        return new PagedResultDto<StockListDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    public async Task<List<StockListDto>> GetBySucursalAsync(int sucursalId, int? userSucursalId, bool isAdmin)
    {
        if (!isAdmin && userSucursalId != sucursalId)
        {
            return new List<StockListDto>();
        }

        return await _context.Stocks
            .Include(s => s.Producto)
                .ThenInclude(p => p.Categoria)
            .Include(s => s.Sucursal)
            .AsNoTracking()
            .Where(s => s.SucursalId == sucursalId)
            .OrderBy(s => s.Producto.Nombre)
            .Select(s => new StockListDto
            {
                Id = s.Id,
                ProductoId = s.ProductoId,
                ProductoCodigo = s.Producto.Codigo,
                ProductoNombre = s.Producto.Nombre,
                ProductoPrecio = s.Producto.Precio,
                CategoriaNombre = s.Producto.Categoria.Nombre,
                SucursalId = s.SucursalId,
                SucursalNombre = s.Sucursal.Nombre,
                Cantidad = s.Cantidad,
                StockMinimo = s.StockMinimo
            })
            .ToListAsync();
    }

    public async Task<StockDetailDto?> GetByIdAsync(int id, int? userSucursalId, bool isAdmin)
    {
        var stock = await _context.Stocks
            .Include(s => s.Producto)
                .ThenInclude(p => p.Categoria)
            .Include(s => s.Sucursal)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);

        if (stock == null) return null;

        if (!isAdmin && userSucursalId != stock.SucursalId)
        {
            return null; // No autorizado a ver stock de otra sucursal
        }

        return new StockDetailDto
        {
            Id = stock.Id,
            ProductoId = stock.ProductoId,
            ProductoCodigo = stock.Producto.Codigo,
            ProductoNombre = stock.Producto.Nombre,
            ProductoPrecio = stock.Producto.Precio,
            CategoriaNombre = stock.Producto.Categoria.Nombre,
            SucursalId = stock.SucursalId,
            SucursalNombre = stock.Sucursal.Nombre,
            Cantidad = stock.Cantidad,
            StockMinimo = stock.StockMinimo
        };
    }

    public async Task<(StockDetailDto? Created, string? Error)> CreateAsync(StockCreateDto dto)
    {
        if (dto.Cantidad < 0)
        {
            return (null, "La cantidad no puede ser negativa.");
        }

        if (dto.StockMinimo < 0)
        {
            return (null, "El stock mínimo no puede ser negativo.");
        }

        var producto = await _context.Productos
            .Include(p => p.Categoria)
            .FirstOrDefaultAsync(p => p.Id == dto.ProductoId);
        if (producto == null)
        {
            return (null, $"El producto con ID {dto.ProductoId} no existe.");
        }

        var sucursal = await _context.Sucursales.FindAsync(dto.SucursalId);
        if (sucursal == null)
        {
            return (null, $"La sucursal con ID {dto.SucursalId} no existe.");
        }

        // Verificar si ya existe registro de stock para esta combinación
        var existeStock = await _context.Stocks
            .AnyAsync(s => s.ProductoId == dto.ProductoId && s.SucursalId == dto.SucursalId);
        if (existeStock)
        {
            return (null, $"Ya existe un registro de stock para el producto '{producto.Nombre}' en la sucursal '{sucursal.Nombre}'.");
        }

        var stock = new Stock
        {
            ProductoId = dto.ProductoId,
            SucursalId = dto.SucursalId,
            Cantidad = dto.Cantidad,
            StockMinimo = dto.StockMinimo
        };

        _context.Stocks.Add(stock);
        await _context.SaveChangesAsync();

        var detail = new StockDetailDto
        {
            Id = stock.Id,
            ProductoId = stock.ProductoId,
            ProductoCodigo = producto.Codigo,
            ProductoNombre = producto.Nombre,
            ProductoPrecio = producto.Precio,
            CategoriaNombre = producto.Categoria.Nombre,
            SucursalId = stock.SucursalId,
            SucursalNombre = sucursal.Nombre,
            Cantidad = stock.Cantidad,
            StockMinimo = stock.StockMinimo
        };

        return (detail, null);
    }

    public async Task<(StockDetailDto? Updated, string? Error, bool NotFound)> AjustarAsync(int id, StockAjusteDto dto)
    {
        if (dto.Cantidad < 0)
        {
            return (null, "La cantidad de stock no puede ser inferior a cero.", false);
        }

        if (dto.StockMinimo < 0)
        {
            return (null, "El stock mínimo no puede ser inferior a cero.", false);
        }

        var stock = await _context.Stocks
            .Include(s => s.Producto)
                .ThenInclude(p => p.Categoria)
            .Include(s => s.Sucursal)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (stock == null)
        {
            return (null, null, true);
        }

        stock.Cantidad = dto.Cantidad;
        stock.StockMinimo = dto.StockMinimo;

        await _context.SaveChangesAsync();

        var detail = new StockDetailDto
        {
            Id = stock.Id,
            ProductoId = stock.ProductoId,
            ProductoCodigo = stock.Producto.Codigo,
            ProductoNombre = stock.Producto.Nombre,
            ProductoPrecio = stock.Producto.Precio,
            CategoriaNombre = stock.Producto.Categoria.Nombre,
            SucursalId = stock.SucursalId,
            SucursalNombre = stock.Sucursal.Nombre,
            Cantidad = stock.Cantidad,
            StockMinimo = stock.StockMinimo
        };

        return (detail, null, false);
    }
}
