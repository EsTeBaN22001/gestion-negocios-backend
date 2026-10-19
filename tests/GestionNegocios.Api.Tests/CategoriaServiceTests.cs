using GestionNegocios.Api.Data;
using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Models;
using GestionNegocios.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestionNegocios.Api.Tests;

public class CategoriaServiceTests
{
    private ApplicationDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ReturnsDuplicateFlag()
    {
        using var context = CreateInMemoryDbContext(nameof(CreateAsync_DuplicateName_ReturnsDuplicateFlag));
        var service = new CategoriaService(context, NullLogger<CategoriaService>.Instance);

        context.Categorias.Add(new Categoria { Nombre = "Bebidas", Activa = true });
        await context.SaveChangesAsync();

        var (created, duplicateName) = await service.CreateAsync(new CategoriaCreateDto
        {
            Nombre = "bebidas", // Case-insensitive check
            Descripcion = "Gaseosas y jugos"
        });

        Assert.True(duplicateName);
        Assert.Null(created);
    }

    [Fact]
    public async Task CreateAsync_ValidName_CreatesSuccessfully()
    {
        using var context = CreateInMemoryDbContext(nameof(CreateAsync_ValidName_CreatesSuccessfully));
        var service = new CategoriaService(context, NullLogger<CategoriaService>.Instance);

        var (created, duplicateName) = await service.CreateAsync(new CategoriaCreateDto
        {
            Nombre = "Lácteos",
            Descripcion = "Leches y quesos"
        });

        Assert.False(duplicateName);
        Assert.NotNull(created);
        Assert.Equal("Lácteos", created.Nombre);
    }

    [Fact]
    public async Task DeleteAsync_WithProducts_DeactivatesCategoria()
    {
        using var context = CreateInMemoryDbContext(nameof(DeleteAsync_WithProducts_DeactivatesCategoria));
        var service = new CategoriaService(context, NullLogger<CategoriaService>.Instance);

        var categoria = new Categoria { Nombre = "Almacén", Activa = true };
        context.Categorias.Add(categoria);
        await context.SaveChangesAsync();

        var producto = new Producto
        {
            Nombre = "Arroz",
            Codigo = "ARR001",
            Precio = 1500,
            CategoriaId = categoria.Id
        };
        context.Productos.Add(producto);
        await context.SaveChangesAsync();

        var (success, deactivated, message) = await service.DeleteAsync(categoria.Id);

        Assert.True(success);
        Assert.True(deactivated);
        Assert.Contains("desactivada", message);

        var inDb = await context.Categorias.FindAsync(categoria.Id);
        Assert.False(inDb!.Activa);
    }
}
