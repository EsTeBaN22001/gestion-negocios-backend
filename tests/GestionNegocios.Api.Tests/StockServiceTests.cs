using GestionNegocios.Api.Data;
using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Models;
using GestionNegocios.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionNegocios.Api.Tests;

public class StockServiceTests
{
    private ApplicationDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_DuplicateProductBranch_ReturnsError()
    {
        using var context = CreateInMemoryDbContext(nameof(CreateAsync_DuplicateProductBranch_ReturnsError));
        var service = new StockService(context);

        var cat = new Categoria { Id = 1, Nombre = "Cat" };
        var prod = new Producto { Id = 1, Codigo = "P-1", Nombre = "Prod 1", Precio = 10, CategoriaId = 1 };
        var suc = new Sucursal { Id = 1, Nombre = "Suc 1", Activa = true };
        var stockExistente = new Stock { Id = 1, ProductoId = 1, SucursalId = 1, Cantidad = 5, StockMinimo = 2 };

        context.Categorias.Add(cat);
        context.Productos.Add(prod);
        context.Sucursales.Add(suc);
        context.Stocks.Add(stockExistente);
        await context.SaveChangesAsync();

        var (created, error) = await service.CreateAsync(new StockCreateDto
        {
            ProductoId = 1,
            SucursalId = 1,
            Cantidad = 10,
            StockMinimo = 5
        });

        Assert.Null(created);
        Assert.NotNull(error);
        Assert.Contains("ya existe un registro de stock", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_NegativeQuantity_ReturnsError()
    {
        using var context = CreateInMemoryDbContext(nameof(CreateAsync_NegativeQuantity_ReturnsError));
        var service = new StockService(context);

        var (created, error) = await service.CreateAsync(new StockCreateDto
        {
            ProductoId = 1,
            SucursalId = 1,
            Cantidad = -5,
            StockMinimo = 2
        });

        Assert.Null(created);
        Assert.NotNull(error);
        Assert.Contains("no puede ser negativa", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetPagedAsync_ForEmpleado_ForcesUserSucursal()
    {
        using var context = CreateInMemoryDbContext(nameof(GetPagedAsync_ForEmpleado_ForcesUserSucursal));
        var service = new StockService(context);

        var cat = new Categoria { Id = 1, Nombre = "General" };
        var prod = new Producto { Id = 1, Codigo = "P-1", Nombre = "Item 1", Precio = 100, CategoriaId = 1 };
        var suc1 = new Sucursal { Id = 1, Nombre = "Sucursal Norte", Activa = true };
        var suc2 = new Sucursal { Id = 2, Nombre = "Sucursal Sur", Activa = true };

        context.Categorias.Add(cat);
        context.Productos.Add(prod);
        context.Sucursales.AddRange(suc1, suc2);
        context.Stocks.AddRange(
            new Stock { Id = 1, ProductoId = 1, SucursalId = 1, Cantidad = 10, StockMinimo = 2 },
            new Stock { Id = 2, ProductoId = 1, SucursalId = 2, Cantidad = 25, StockMinimo = 5 }
        );
        await context.SaveChangesAsync();

        // Empleado pertenece a la sucursal 1 pero intenta consultar la sucursal 2
        var result = await service.GetPagedAsync(
            page: 1,
            pageSize: 10,
            sucursalId: 2, // Intentando filtrar sucursal 2
            search: null,
            soloBajoStock: null,
            userSucursalId: 1, // Su sucursal real es 1
            isAdmin: false);

        Assert.Equal(1, result.TotalItems);
        Assert.Equal(1, result.Items.First().SucursalId);
        Assert.Equal("Sucursal Norte", result.Items.First().SucursalNombre);
    }

    [Fact]
    public async Task GetPagedAsync_SoloBajoStock_FiltersCorrectly()
    {
        using var context = CreateInMemoryDbContext(nameof(GetPagedAsync_SoloBajoStock_FiltersCorrectly));
        var service = new StockService(context);

        var cat = new Categoria { Id = 1, Nombre = "General" };
        var prod1 = new Producto { Id = 1, Codigo = "P-1", Nombre = "Item Normal", Precio = 100, CategoriaId = 1 };
        var prod2 = new Producto { Id = 2, Codigo = "P-2", Nombre = "Item Bajo", Precio = 100, CategoriaId = 1 };
        var suc = new Sucursal { Id = 1, Nombre = "Central", Activa = true };

        context.Categorias.Add(cat);
        context.Productos.AddRange(prod1, prod2);
        context.Sucursales.Add(suc);
        context.Stocks.AddRange(
            new Stock { Id = 1, ProductoId = 1, SucursalId = 1, Cantidad = 50, StockMinimo = 10 }, // Normal
            new Stock { Id = 2, ProductoId = 2, SucursalId = 1, Cantidad = 4, StockMinimo = 10 }   // Bajo
        );
        await context.SaveChangesAsync();

        var result = await service.GetPagedAsync(
            page: 1,
            pageSize: 10,
            sucursalId: 1,
            search: null,
            soloBajoStock: true,
            userSucursalId: null,
            isAdmin: true);

        Assert.Equal(1, result.TotalItems);
        Assert.Equal("Item Bajo", result.Items.First().ProductoNombre);
        Assert.Equal("Bajo", result.Items.First().AlertaStock);
    }

    [Fact]
    public async Task AjustarAsync_Valid_UpdatesQuantityAndMin()
    {
        using var context = CreateInMemoryDbContext(nameof(AjustarAsync_Valid_UpdatesQuantityAndMin));
        var service = new StockService(context);

        var cat = new Categoria { Id = 1, Nombre = "General" };
        var prod = new Producto { Id = 1, Codigo = "P-1", Nombre = "Item", Precio = 50, CategoriaId = 1 };
        var suc = new Sucursal { Id = 1, Nombre = "Central", Activa = true };
        var stock = new Stock { Id = 1, ProductoId = 1, SucursalId = 1, Cantidad = 5, StockMinimo = 5 };

        context.Categorias.Add(cat);
        context.Productos.Add(prod);
        context.Sucursales.Add(suc);
        context.Stocks.Add(stock);
        await context.SaveChangesAsync();

        var (updated, error, notFound) = await service.AjustarAsync(1, new StockAjusteDto
        {
            Cantidad = 25,
            StockMinimo = 8,
            Motivo = "Reposición de mercadería semanal"
        });

        Assert.False(notFound);
        Assert.Null(error);
        Assert.NotNull(updated);
        Assert.Equal(25, updated.Cantidad);
        Assert.Equal(8, updated.StockMinimo);
        Assert.Equal("Normal", updated.AlertaStock);
    }
}
