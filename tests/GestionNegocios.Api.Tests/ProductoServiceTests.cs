using GestionNegocios.Api.Data;
using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Models;
using GestionNegocios.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GestionNegocios.Api.Tests;

public class ProductoServiceTests
{
    private ApplicationDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_ReturnsError()
    {
        using var context = CreateInMemoryDbContext(nameof(CreateAsync_DuplicateCode_ReturnsError));
        var service = new ProductoService(context);

        context.Categorias.Add(new Categoria { Id = 1, Nombre = "Bebidas" });
        context.Productos.Add(new Producto
        {
            Id = 1,
            Codigo = "PROD-001",
            Nombre = "Coca Cola 1.5L",
            Precio = 1500,
            CategoriaId = 1,
            Activo = true
        });
        await context.SaveChangesAsync();

        var (created, error) = await service.CreateAsync(new ProductoCreateDto
        {
            Codigo = "prod-001", // Mismo código en minúsculas
            Nombre = "Coca Cola 2.25L",
            Precio = 2000,
            CategoriaId = 1
        });

        Assert.Null(created);
        Assert.NotNull(error);
        Assert.Contains("ya existe un producto", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_InvalidCategory_ReturnsError()
    {
        using var context = CreateInMemoryDbContext(nameof(CreateAsync_InvalidCategory_ReturnsError));
        var service = new ProductoService(context);

        var (created, error) = await service.CreateAsync(new ProductoCreateDto
        {
            Codigo = "PROD-999",
            Nombre = "Producto sin categoría",
            Precio = 500,
            CategoriaId = 999
        });

        Assert.Null(created);
        Assert.NotNull(error);
        Assert.Contains("no existe", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_Valid_CreatesProductSuccessfully()
    {
        using var context = CreateInMemoryDbContext(nameof(CreateAsync_Valid_CreatesProductSuccessfully));
        var service = new ProductoService(context);

        context.Categorias.Add(new Categoria { Id = 1, Nombre = "Alimentos" });
        await context.SaveChangesAsync();

        var (created, error) = await service.CreateAsync(new ProductoCreateDto
        {
            Codigo = "ALIM-001",
            Nombre = "Galletitas de Chocolate",
            Descripcion = "Paquete 250g",
            Precio = 850.50m,
            CategoriaId = 1
        });

        Assert.Null(error);
        Assert.NotNull(created);
        Assert.Equal("ALIM-001", created.Codigo);
        Assert.Equal("Galletitas de Chocolate", created.Nombre);
        Assert.Equal("Alimentos", created.CategoriaNombre);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersAndPagination_WorkProperly()
    {
        using var context = CreateInMemoryDbContext(nameof(GetPagedAsync_FiltersAndPagination_WorkProperly));
        var service = new ProductoService(context);

        var cat1 = new Categoria { Id = 1, Nombre = "Bebidas" };
        var cat2 = new Categoria { Id = 2, Nombre = "Golosinas" };
        context.Categorias.AddRange(cat1, cat2);

        context.Productos.AddRange(
            new Producto { Id = 1, Codigo = "B-01", Nombre = "Agua Mineral", Precio = 500, CategoriaId = 1, Activo = true },
            new Producto { Id = 2, Codigo = "B-02", Nombre = "Jugo Naranja", Precio = 700, CategoriaId = 1, Activo = true },
            new Producto { Id = 3, Codigo = "G-01", Nombre = "Alfajor Dulce de Leche", Precio = 600, CategoriaId = 2, Activo = true }
        );
        await context.SaveChangesAsync();

        // Filtrar por categoría 1
        var pagedCat1 = await service.GetPagedAsync(1, 10, null, 1, null);
        Assert.Equal(2, pagedCat1.TotalItems);

        // Búsqueda por texto "Alfajor"
        var pagedSearch = await service.GetPagedAsync(1, 10, "Alfajor", null, null);
        Assert.Equal(1, pagedSearch.TotalItems);
        Assert.Equal("Alfajor Dulce de Leche", pagedSearch.Items.First().Nombre);
    }

    [Fact]
    public async Task DeleteAsync_WithStock_DeactivatesProduct()
    {
        using var context = CreateInMemoryDbContext(nameof(DeleteAsync_WithStock_DeactivatesProduct));
        var service = new ProductoService(context);

        var cat = new Categoria { Id = 1, Nombre = "General" };
        var suc = new Sucursal { Id = 1, Nombre = "Central", Activa = true };
        var prod = new Producto { Id = 1, Codigo = "GEN-01", Nombre = "Item con stock", Precio = 100, CategoriaId = 1, Activo = true };
        var stock = new Stock { Id = 1, ProductoId = 1, SucursalId = 1, Cantidad = 10, StockMinimo = 2 };

        context.Categorias.Add(cat);
        context.Sucursales.Add(suc);
        context.Productos.Add(prod);
        context.Stocks.Add(stock);
        await context.SaveChangesAsync();

        var (success, error, deactivated, notFound) = await service.DeleteAsync(1);

        Assert.True(success);
        Assert.True(deactivated);
        Assert.False(notFound);

        var dbProd = await context.Productos.FindAsync(1);
        Assert.NotNull(dbProd);
        Assert.False(dbProd.Activo);
    }
}
