using GestionNegocios.Api.Data;
using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Models;
using GestionNegocios.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionNegocios.Api.Tests;

public class VentaServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task SeedBaseDataAsync(ApplicationDbContext context)
    {
        var categoria = new Categoria { Id = 1, Nombre = "Bebidas", Activa = true };
        var productoUno = new Producto
        {
            Id = 1,
            Codigo = "BEB-001",
            Nombre = "Agua",
            Precio = 100m,
            CategoriaId = 1,
            Activo = true
        };
        var productoDos = new Producto
        {
            Id = 2,
            Codigo = "BEB-002",
            Nombre = "Jugo",
            Precio = 250m,
            CategoriaId = 1,
            Activo = true
        };
        var sucursalUno = new Sucursal { Id = 1, Nombre = "Central", Activa = true };
        var sucursalDos = new Sucursal { Id = 2, Nombre = "Norte", Activa = true };
        var empleado = new Usuario
        {
            Id = 1,
            Nombre = "Elena",
            Apellido = "Paz",
            Email = "elena@example.test",
            PasswordHash = "hash",
            Rol = "Empleado",
            SucursalId = 1,
            Activo = true
        };
        var administrador = new Usuario
        {
            Id = 2,
            Nombre = "Ana",
            Apellido = "Admin",
            Email = "ana@example.test",
            PasswordHash = "hash",
            Rol = "Administrador",
            Activo = true
        };

        context.Categorias.Add(categoria);
        context.Productos.AddRange(productoUno, productoDos);
        context.Sucursales.AddRange(sucursalUno, sucursalDos);
        context.Usuarios.AddRange(empleado, administrador);
        context.Stocks.AddRange(
            new Stock { Id = 1, ProductoId = 1, SucursalId = 1, Cantidad = 10, StockMinimo = 2 },
            new Stock { Id = 2, ProductoId = 2, SucursalId = 1, Cantidad = 6, StockMinimo = 2 },
            new Stock { Id = 3, ProductoId = 1, SucursalId = 2, Cantidad = 8, StockMinimo = 2 });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateAsync_ValidSale_CreatesSaleAndDeductsStock()
    {
        using var context = CreateInMemoryDbContext(nameof(CreateAsync_ValidSale_CreatesSaleAndDeductsStock));
        await SeedBaseDataAsync(context);
        var service = new VentaService(context, null!);

        var result = await service.CreateAsync(new VentaCreateDto
        {
            Detalles =
            [
                new VentaDetalleCreateDto { ProductoId = 1, Cantidad = 2 },
                new VentaDetalleCreateDto { ProductoId = 2, Cantidad = 1 }
            ]
        }, userId: 1, userSucursalId: 1, isAdmin: false);

        Assert.Equal(VentaResultStatus.Success, result.Status);
        Assert.NotNull(result.Venta);
        Assert.Equal("Confirmada", result.Venta.Estado);
        Assert.Equal(450m, result.Venta.Total);
        Assert.Equal(2, result.Venta.Detalles.Count);
        Assert.Equal(8, (await context.Stocks.FindAsync(1))!.Cantidad);
        Assert.Equal(5, (await context.Stocks.FindAsync(2))!.Cantidad);
        Assert.Single(context.Ventas);
    }

    [Fact]
    public async Task CreateAsync_InsufficientStock_DoesNotPersistOrModifyStock()
    {
        using var context = CreateInMemoryDbContext(nameof(CreateAsync_InsufficientStock_DoesNotPersistOrModifyStock));
        await SeedBaseDataAsync(context);
        var service = new VentaService(context, null!);

        var result = await service.CreateAsync(new VentaCreateDto
        {
            Detalles = [new VentaDetalleCreateDto { ProductoId = 1, Cantidad = 11 }]
        }, userId: 1, userSucursalId: 1, isAdmin: false);

        Assert.Equal(VentaResultStatus.Conflict, result.Status);
        Assert.Contains("Stock insuficiente", result.Error);
        Assert.Empty(context.Ventas);
        Assert.Equal(10, (await context.Stocks.FindAsync(1))!.Cantidad);
    }

    [Fact]
    public async Task CreateAsync_RepeatedProduct_ReturnsValidationError()
    {
        using var context = CreateInMemoryDbContext(nameof(CreateAsync_RepeatedProduct_ReturnsValidationError));
        await SeedBaseDataAsync(context);
        var service = new VentaService(context, null!);

        var result = await service.CreateAsync(new VentaCreateDto
        {
            Detalles =
            [
                new VentaDetalleCreateDto { ProductoId = 1, Cantidad = 1 },
                new VentaDetalleCreateDto { ProductoId = 1, Cantidad = 1 }
            ]
        }, userId: 1, userSucursalId: 1, isAdmin: false);

        Assert.Equal(VentaResultStatus.ValidationError, result.Status);
        Assert.Contains("repetidos", result.Error);
        Assert.Empty(context.Ventas);
    }

    [Fact]
    public async Task CreateAsync_EmployeeSucursalIsForcedFromClaims()
    {
        using var context = CreateInMemoryDbContext(nameof(CreateAsync_EmployeeSucursalIsForcedFromClaims));
        await SeedBaseDataAsync(context);
        var service = new VentaService(context, null!);

        var result = await service.CreateAsync(new VentaCreateDto
        {
            SucursalId = 2,
            Detalles = [new VentaDetalleCreateDto { ProductoId = 1, Cantidad = 2 }]
        }, userId: 1, userSucursalId: 1, isAdmin: false);

        Assert.Equal(VentaResultStatus.Success, result.Status);
        Assert.Equal(1, result.Venta!.SucursalId);
        Assert.Equal(8, (await context.Stocks.FindAsync(1))!.Cantidad);
        Assert.Equal(8, (await context.Stocks.FindAsync(3))!.Cantidad);
    }

    [Fact]
    public async Task CreateAsync_PreservesHistoricalUnitPrice()
    {
        using var context = CreateInMemoryDbContext(nameof(CreateAsync_PreservesHistoricalUnitPrice));
        await SeedBaseDataAsync(context);
        var service = new VentaService(context, null!);

        var created = await service.CreateAsync(new VentaCreateDto
        {
            Detalles = [new VentaDetalleCreateDto { ProductoId = 1, Cantidad = 2 }]
        }, userId: 1, userSucursalId: 1, isAdmin: false);

        var producto = (await context.Productos.FindAsync(1))!;
        producto.Precio = 150m;
        await context.SaveChangesAsync();

        var result = await service.GetByIdAsync(created.Venta!.Id, userSucursalId: 1, isAdmin: false);

        Assert.Equal(VentaResultStatus.Success, result.Status);
        Assert.Equal(100m, result.Venta!.Detalles.Single().PrecioUnitario);
        Assert.Equal(200m, result.Venta.Total);
    }

    [Fact]
    public async Task AnularAsync_ConfirmedSale_RestoresStockAndKeepsHistory()
    {
        using var context = CreateInMemoryDbContext(nameof(AnularAsync_ConfirmedSale_RestoresStockAndKeepsHistory));
        await SeedBaseDataAsync(context);
        var service = new VentaService(context, null!);
        var created = await service.CreateAsync(new VentaCreateDto
        {
            Detalles = [new VentaDetalleCreateDto { ProductoId = 1, Cantidad = 3 }]
        }, userId: 1, userSucursalId: 1, isAdmin: false);

        var result = await service.AnularAsync(created.Venta!.Id, userSucursalId: 1, isAdmin: false);

        Assert.Equal(VentaResultStatus.Success, result.Status);
        Assert.Equal("Anulada", result.Venta!.Estado);
        Assert.Equal(10, (await context.Stocks.FindAsync(1))!.Cantidad);
        Assert.Single(context.Ventas);
        Assert.Single(context.DetallesVenta);
    }

    [Fact]
    public async Task AnularAsync_AlreadyCancelledSale_ReturnsConflictWithoutRestoringTwice()
    {
        using var context = CreateInMemoryDbContext(nameof(AnularAsync_AlreadyCancelledSale_ReturnsConflictWithoutRestoringTwice));
        await SeedBaseDataAsync(context);
        var service = new VentaService(context, null!);
        var created = await service.CreateAsync(new VentaCreateDto
        {
            Detalles = [new VentaDetalleCreateDto { ProductoId = 1, Cantidad = 3 }]
        }, userId: 1, userSucursalId: 1, isAdmin: false);

        await service.AnularAsync(created.Venta!.Id, userSucursalId: 1, isAdmin: false);
        var result = await service.AnularAsync(created.Venta.Id, userSucursalId: 1, isAdmin: false);

        Assert.Equal(VentaResultStatus.Conflict, result.Status);
        Assert.Equal(10, (await context.Stocks.FindAsync(1))!.Cantidad);
    }

    [Fact]
    public async Task GetPagedAsync_EmployeeCanOnlySeeOwnSucursal()
    {
        using var context = CreateInMemoryDbContext(nameof(GetPagedAsync_EmployeeCanOnlySeeOwnSucursal));
        await SeedBaseDataAsync(context);
        var service = new VentaService(context, null!);

        await service.CreateAsync(new VentaCreateDto
        {
            Detalles = [new VentaDetalleCreateDto { ProductoId = 1, Cantidad = 1 }]
        }, userId: 1, userSucursalId: 1, isAdmin: false);
        await service.CreateAsync(new VentaCreateDto
        {
            SucursalId = 2,
            Detalles = [new VentaDetalleCreateDto { ProductoId = 1, Cantidad = 1 }]
        }, userId: 2, userSucursalId: null, isAdmin: true);

        var result = await service.GetPagedAsync(
            sucursalId: 2,
            userSucursalId: 1,
            isAdmin: false);

        Assert.Single(result.Items);
        Assert.Equal(1, result.Items.Single().SucursalId);
    }

    [Fact]
    public async Task AdjuntarArchivoAsync_EmpleadoDifferentSucursal_ReturnsForbidden()
    {
        using var context = CreateInMemoryDbContext(nameof(AdjuntarArchivoAsync_EmpleadoDifferentSucursal_ReturnsForbidden));
        await SeedBaseDataAsync(context);
        var fakeStorage = new FakeFileStorageService();
        var service = new VentaService(context, fakeStorage);

        // Venta en sucursal 2
        var created = await service.CreateAsync(new VentaCreateDto
        {
            SucursalId = 2,
            Detalles = [new VentaDetalleCreateDto { ProductoId = 1, Cantidad = 1 }]
        }, userId: 2, userSucursalId: null, isAdmin: true);

        var formFile = new Microsoft.AspNetCore.Http.FormFile(new MemoryStream([1]), 0, 1, "file", "ticket.pdf");

        // Empleado de sucursal 1 intenta adjuntar comprobante a venta de sucursal 2
        var result = await service.AdjuntarArchivoAsync(created.Venta!.Id, formFile, userSucursalId: 1, isAdmin: false);

        Assert.Equal(VentaResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task AdjuntarArchivoAsync_ValidSale_SavesAttachmentAndReplacesOld()
    {
        using var context = CreateInMemoryDbContext(nameof(AdjuntarArchivoAsync_ValidSale_SavesAttachmentAndReplacesOld));
        await SeedBaseDataAsync(context);
        var fakeStorage = new FakeFileStorageService
        {
            RelativePathToReturn = "/uploads/ventas/factura.pdf"
        };

        var service = new VentaService(context, fakeStorage);

        var created = await service.CreateAsync(new VentaCreateDto
        {
            Detalles = [new VentaDetalleCreateDto { ProductoId = 1, Cantidad = 1 }]
        }, userId: 1, userSucursalId: 1, isAdmin: false);

        var formFile = new Microsoft.AspNetCore.Http.FormFile(new MemoryStream([1]), 0, 1, "file", "factura.pdf")
        {
            Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
            ContentType = "application/pdf"
        };
        var result = await service.AdjuntarArchivoAsync(created.Venta!.Id, formFile, userSucursalId: 1, isAdmin: false);

        Assert.Equal(VentaResultStatus.Success, result.Status);
        Assert.Equal("/uploads/ventas/factura.pdf", result.Venta!.ArchivoAdjunto);
    }

    [Fact]
    public async Task AnularAsync_PreservesArchivoAdjunto()
    {
        using var context = CreateInMemoryDbContext(nameof(AnularAsync_PreservesArchivoAdjunto));
        await SeedBaseDataAsync(context);
        var fakeStorage = new FakeFileStorageService();
        var service = new VentaService(context, fakeStorage);

        var created = await service.CreateAsync(new VentaCreateDto
        {
            Detalles = [new VentaDetalleCreateDto { ProductoId = 1, Cantidad = 1 }]
        }, userId: 1, userSucursalId: 1, isAdmin: false);

        var venta = await context.Ventas.FindAsync(created.Venta!.Id);
        venta!.ArchivoAdjunto = "/uploads/ventas/comprobante_historico.pdf";
        await context.SaveChangesAsync();

        var anularResult = await service.AnularAsync(venta.Id, userSucursalId: 1, isAdmin: false);

        Assert.Equal(VentaResultStatus.Success, anularResult.Status);
        var reloaded = await context.Ventas.FindAsync(venta.Id);
        Assert.Equal("Anulada", reloaded!.Estado);
        Assert.Equal("/uploads/ventas/comprobante_historico.pdf", reloaded.ArchivoAdjunto);
        // Comprobar que no se eliminó el archivo en disco
        Assert.Empty(fakeStorage.DeletedFiles);
    }
}
