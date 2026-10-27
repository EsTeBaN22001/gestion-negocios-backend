using GestionNegocios.Api.Data;
using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionNegocios.Api.Services;

public class VentaService : IVentaService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;

    private static readonly string[] AllowedVentaExtensions = [".pdf", ".jpg", ".jpeg", ".png"];
    private static readonly string[] AllowedVentaMimeTypes = ["application/pdf", "image/jpeg", "image/png"];
    private const long MaxVentaSizeBytes = 5 * 1024 * 1024; // 5 MB

    public VentaService(ApplicationDbContext context, IFileStorageService fileStorageService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
    }

    public async Task<PagedResultDto<VentaListDto>> GetPagedAsync(
        int page = 1,
        int pageSize = 10,
        string? search = null,
        int? sucursalId = null,
        int? usuarioId = null,
        string? estado = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        int? userSucursalId = null,
        bool isAdmin = false)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Ventas.AsNoTracking().AsQueryable();

        if (!isAdmin)
        {
            if (!userSucursalId.HasValue)
            {
                return new PagedResultDto<VentaListDto>
                {
                    Items = [],
                    Page = page,
                    PageSize = pageSize,
                    TotalItems = 0,
                    TotalPages = 0
                };
            }

            query = query.Where(v => v.SucursalId == userSucursalId.Value);
        }
        else if (sucursalId is > 0)
        {
            query = query.Where(v => v.SucursalId == sucursalId.Value);
        }

        if (usuarioId is > 0)
        {
            query = query.Where(v => v.UsuarioId == usuarioId.Value);
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            var estadoNormalizado = estado.Trim();
            query = query.Where(v => v.Estado == estadoNormalizado);
        }

        if (fechaDesde.HasValue)
        {
            var inicio = fechaDesde.Value.Date;
            query = query.Where(v => v.Fecha >= inicio);
        }

        if (fechaHasta.HasValue)
        {
            var finExclusivo = fechaHasta.Value.Date.AddDays(1);
            query = query.Where(v => v.Fecha < finExclusivo);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(v =>
                EF.Functions.Like(v.Sucursal.Nombre, $"%{term}%") ||
                EF.Functions.Like(v.Usuario.Nombre, $"%{term}%") ||
                EF.Functions.Like(v.Usuario.Apellido, $"%{term}%") ||
                v.Detalles.Any(d =>
                    EF.Functions.Like(d.Producto.Nombre, $"%{term}%") ||
                    EF.Functions.Like(d.Producto.Codigo, $"%{term}%")));
        }

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var items = await query
            .OrderByDescending(v => v.Fecha)
            .ThenByDescending(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new VentaListDto
            {
                Id = v.Id,
                Fecha = v.Fecha,
                Total = v.Total,
                Estado = v.Estado,
                SucursalId = v.SucursalId,
                SucursalNombre = v.Sucursal.Nombre,
                UsuarioId = v.UsuarioId,
                UsuarioNombre = v.Usuario.Nombre + " " + v.Usuario.Apellido,
                DetallesCount = v.Detalles.Count,
                ArchivoAdjunto = v.ArchivoAdjunto
            })
            .ToListAsync();

        return new PagedResultDto<VentaListDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }

    public async Task<VentaServiceResult> GetByIdAsync(int id, int? userSucursalId, bool isAdmin)
    {
        var venta = await GetVentaWithDetailsAsync(id);
        if (venta == null)
        {
            return new VentaServiceResult(VentaResultStatus.NotFound, Error: "La venta no existe.");
        }

        if (!isAdmin && userSucursalId != venta.SucursalId)
        {
            return new VentaServiceResult(VentaResultStatus.Forbidden, Error: "No tiene permiso para consultar esta venta.");
        }

        return new VentaServiceResult(VentaResultStatus.Success, MapDetail(venta));
    }

    public async Task<VentaServiceResult> CreateAsync(
        VentaCreateDto dto,
        int userId,
        int? userSucursalId,
        bool isAdmin)
    {
        if (dto.Detalles.Count == 0)
        {
            return new VentaServiceResult(VentaResultStatus.ValidationError, Error: "La venta debe incluir al menos un detalle.");
        }

        if (dto.Detalles.Any(d => d.ProductoId <= 0 || d.Cantidad <= 0))
        {
            return new VentaServiceResult(VentaResultStatus.ValidationError, Error: "Cada detalle debe indicar un producto y una cantidad mayor a cero.");
        }

        if (dto.Detalles.GroupBy(d => d.ProductoId).Any(group => group.Count() > 1))
        {
            return new VentaServiceResult(VentaResultStatus.ValidationError, Error: "No se permiten productos repetidos en una misma venta.");
        }

        if (isAdmin && (!dto.SucursalId.HasValue || dto.SucursalId.Value <= 0))
        {
            return new VentaServiceResult(VentaResultStatus.ValidationError, Error: "Debe seleccionar una sucursal para registrar la venta.");
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync()
                : null;

            try
            {
                var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == userId && u.Activo);
                if (usuario == null)
                {
                    return new VentaServiceResult(VentaResultStatus.Forbidden, Error: "El usuario autenticado no está habilitado para registrar ventas.");
                }

                var sucursalId = isAdmin ? dto.SucursalId : userSucursalId;
                if (!sucursalId.HasValue || sucursalId.Value <= 0)
                {
                    return new VentaServiceResult(VentaResultStatus.Forbidden, Error: "No existe una sucursal asignada para registrar la venta.");
                }

                var sucursal = await _context.Sucursales.FirstOrDefaultAsync(s => s.Id == sucursalId.Value && s.Activa);
                if (sucursal == null)
                {
                    return new VentaServiceResult(VentaResultStatus.NotFound, Error: "La sucursal indicada no existe o no está activa.");
                }

                var productoIds = dto.Detalles.Select(d => d.ProductoId).ToList();
                var productos = await _context.Productos
                    .Where(p => productoIds.Contains(p.Id) && p.Activo)
                    .ToListAsync();

                if (productos.Count != productoIds.Count)
                {
                    return new VentaServiceResult(VentaResultStatus.NotFound, Error: "Uno o más productos no existen o no están activos.");
                }

                var stocks = await _context.Stocks
                    .Where(s => s.SucursalId == sucursalId.Value && productoIds.Contains(s.ProductoId))
                    .ToListAsync();

                if (stocks.Count != productoIds.Count)
                {
                    return new VentaServiceResult(VentaResultStatus.NotFound, Error: "No existe stock configurado para uno o más productos en la sucursal seleccionada.");
                }

                var stocksByProduct = stocks.ToDictionary(s => s.ProductoId);
                var productosById = productos.ToDictionary(p => p.Id);

                foreach (var detalle in dto.Detalles)
                {
                    if (stocksByProduct[detalle.ProductoId].Cantidad < detalle.Cantidad)
                    {
                        var producto = productosById[detalle.ProductoId];
                        return new VentaServiceResult(
                            VentaResultStatus.Conflict,
                            Error: $"Stock insuficiente para el producto '{producto.Nombre}'.");
                    }
                }

                var venta = new Venta
                {
                    Fecha = DateTime.UtcNow,
                    SucursalId = sucursalId.Value,
                    Sucursal = sucursal,
                    UsuarioId = usuario.Id,
                    Usuario = usuario,
                    Estado = "Confirmada"
                };

                foreach (var detalleDto in dto.Detalles)
                {
                    var producto = productosById[detalleDto.ProductoId];
                    var stock = stocksByProduct[detalleDto.ProductoId];
                    var subtotal = producto.Precio * detalleDto.Cantidad;

                    venta.Detalles.Add(new DetalleVenta
                    {
                        ProductoId = producto.Id,
                        Producto = producto,
                        Cantidad = detalleDto.Cantidad,
                        PrecioUnitario = producto.Precio,
                        Subtotal = subtotal
                    });

                    stock.Cantidad -= detalleDto.Cantidad;
                }

                venta.Total = venta.Detalles.Sum(d => d.Subtotal);
                _context.Ventas.Add(venta);
                await _context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return new VentaServiceResult(VentaResultStatus.Success, MapDetail(venta));
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }

                throw;
            }
        });
    }

    public async Task<VentaServiceResult> AnularAsync(int id, int? userSucursalId, bool isAdmin)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync()
                : null;

            try
            {
                var venta = await GetVentaWithDetailsAsync(id);
                if (venta == null)
                {
                    return new VentaServiceResult(VentaResultStatus.NotFound, Error: "La venta no existe.");
                }

                if (!isAdmin && userSucursalId != venta.SucursalId)
                {
                    return new VentaServiceResult(VentaResultStatus.Forbidden, Error: "No tiene permiso para anular esta venta.");
                }

                if (venta.Estado == "Anulada")
                {
                    return new VentaServiceResult(VentaResultStatus.Conflict, Error: "La venta ya fue anulada.");
                }

                var productoIds = venta.Detalles.Select(d => d.ProductoId).ToList();
                var stocks = await _context.Stocks
                    .Where(s => s.SucursalId == venta.SucursalId && productoIds.Contains(s.ProductoId))
                    .ToListAsync();

                if (stocks.Count != productoIds.Count)
                {
                    return new VentaServiceResult(VentaResultStatus.NotFound, Error: "No existe stock para restaurar uno o más productos de la venta.");
                }

                var stocksByProduct = stocks.ToDictionary(s => s.ProductoId);

                foreach (var detalle in venta.Detalles)
                {
                    stocksByProduct[detalle.ProductoId].Cantidad += detalle.Cantidad;
                }

                venta.Estado = "Anulada";
                await _context.SaveChangesAsync();

                if (transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return new VentaServiceResult(VentaResultStatus.Success, MapDetail(venta));
            }
            catch
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }

                throw;
            }
        });
    }

    private Task<Venta?> GetVentaWithDetailsAsync(int id)
    {
        return _context.Ventas
            .Include(v => v.Sucursal)
            .Include(v => v.Usuario)
            .Include(v => v.Detalles)
                .ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    private static VentaDetailDto MapDetail(Venta venta)
    {
        return new VentaDetailDto
        {
            Id = venta.Id,
            Fecha = venta.Fecha,
            Total = venta.Total,
            Estado = venta.Estado,
            SucursalId = venta.SucursalId,
            SucursalNombre = venta.Sucursal.Nombre,
            UsuarioId = venta.UsuarioId,
            UsuarioNombre = $"{venta.Usuario.Nombre} {venta.Usuario.Apellido}",
            ArchivoAdjunto = venta.ArchivoAdjunto,
            Detalles = venta.Detalles
                .OrderBy(d => d.Id)
                .Select(d => new VentaDetalleDto
                {
                    ProductoId = d.ProductoId,
                    ProductoCodigo = d.Producto.Codigo,
                    ProductoNombre = d.Producto.Nombre,
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario,
                    Subtotal = d.Subtotal
                })
                .ToList()
        };
    }

    public async Task<VentaServiceResult> AdjuntarArchivoAsync(
        int ventaId,
        IFormFile file,
        int? userSucursalId,
        bool isAdmin)
    {
        var venta = await GetVentaWithDetailsAsync(ventaId);
        if (venta == null)
        {
            return new VentaServiceResult(VentaResultStatus.NotFound, Error: "La venta no existe.");
        }

        if (!isAdmin && userSucursalId != venta.SucursalId)
        {
            return new VentaServiceResult(VentaResultStatus.Forbidden, Error: "No tiene permiso para adjuntar archivos a una venta de otra sucursal.");
        }

        var (relativePath, saveError) = await _fileStorageService.SaveFileAsync(
            file,
            "ventas",
            AllowedVentaExtensions,
            AllowedVentaMimeTypes,
            MaxVentaSizeBytes);

        if (saveError != null)
        {
            return new VentaServiceResult(VentaResultStatus.ValidationError, Error: saveError);
        }

        // Si ya existía un comprobante anterior, lo eliminamos del filesystem
        if (!string.IsNullOrWhiteSpace(venta.ArchivoAdjunto))
        {
            _fileStorageService.DeleteFile(venta.ArchivoAdjunto);
        }

        venta.ArchivoAdjunto = relativePath;
        await _context.SaveChangesAsync();

        return new VentaServiceResult(VentaResultStatus.Success, MapDetail(venta));
    }

    public async Task<(VentaServiceResult result, string? physicalPath, string? contentType, string? downloadFileName)> GetAdjuntoAsync(
        int ventaId,
        int? userSucursalId,
        bool isAdmin)
    {
        var venta = await _context.Ventas.FirstOrDefaultAsync(v => v.Id == ventaId);
        if (venta == null)
        {
            return (new VentaServiceResult(VentaResultStatus.NotFound, Error: "La venta no existe."), null, null, null);
        }

        if (!isAdmin && userSucursalId != venta.SucursalId)
        {
            return (new VentaServiceResult(VentaResultStatus.Forbidden, Error: "No tiene permiso para acceder al archivo adjunto de una venta de otra sucursal."), null, null, null);
        }

        if (string.IsNullOrWhiteSpace(venta.ArchivoAdjunto))
        {
            return (new VentaServiceResult(VentaResultStatus.NotFound, Error: "La venta no posee un archivo adjunto."), null, null, null);
        }

        var (exists, physicalPath, contentType, fileName) = _fileStorageService.GetFileInfo(venta.ArchivoAdjunto);
        if (!exists)
        {
            return (new VentaServiceResult(VentaResultStatus.NotFound, Error: "El archivo físico no fue encontrado en el servidor."), null, null, null);
        }

        return (new VentaServiceResult(VentaResultStatus.Success), physicalPath, contentType, fileName);
    }
}
