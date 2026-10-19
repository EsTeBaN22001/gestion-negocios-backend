using GestionNegocios.Api.Data;
using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Models;
using GestionNegocios.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestionNegocios.Api.Tests;

public class SucursalServiceTests
{
    private ApplicationDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsPagedItemsAndAppliesSearch()
    {
        using var context = CreateInMemoryDbContext(nameof(GetPagedAsync_ReturnsPagedItemsAndAppliesSearch));
        var service = new SucursalService(context, NullLogger<SucursalService>.Instance);

        context.Sucursales.AddRange(
            new Sucursal { Nombre = "Sucursal Norte", Direccion = "Av. Libertador 100", Activa = true },
            new Sucursal { Nombre = "Sucursal Sur", Direccion = "Av. Mitre 500", Activa = true },
            new Sucursal { Nombre = "Sucursal Centro", Direccion = "Av. Corrientes 1000", Activa = true }
        );
        await context.SaveChangesAsync();

        var result = await service.GetPagedAsync(page: 1, pageSize: 2, search: "norte");

        Assert.Equal(1, result.TotalItems);
        Assert.Single(result.Items);
        Assert.Equal("Sucursal Norte", result.Items.First().Nombre);
    }

    [Fact]
    public async Task CreateAsync_PersistsAndReturnsSucursalDto()
    {
        using var context = CreateInMemoryDbContext(nameof(CreateAsync_PersistsAndReturnsSucursalDto));
        var service = new SucursalService(context, NullLogger<SucursalService>.Instance);

        var dto = new SucursalCreateDto
        {
            Nombre = "Nueva Sucursal Oeste",
            Direccion = "Rivadavia 2000",
            Telefono = "1234-5678"
        };

        var created = await service.CreateAsync(dto);

        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal("Nueva Sucursal Oeste", created.Nombre);
        Assert.True(created.Activa);

        var inDb = await context.Sucursales.FindAsync(created.Id);
        Assert.NotNull(inDb);
        Assert.Equal("Rivadavia 2000", inDb.Direccion);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFieldsSuccessfully()
    {
        using var context = CreateInMemoryDbContext(nameof(UpdateAsync_UpdatesFieldsSuccessfully));
        var service = new SucursalService(context, NullLogger<SucursalService>.Instance);

        var sucursal = new Sucursal { Nombre = "Viejo Nombre", Activa = true };
        context.Sucursales.Add(sucursal);
        await context.SaveChangesAsync();

        var updateDto = new SucursalUpdateDto
        {
            Nombre = "Nuevo Nombre",
            Direccion = "Nueva Direccion",
            Activa = false
        };

        var updated = await service.UpdateAsync(sucursal.Id, updateDto);

        Assert.NotNull(updated);
        Assert.Equal("Nuevo Nombre", updated.Nombre);
        Assert.False(updated.Activa);

        var inDb = await context.Sucursales.FindAsync(sucursal.Id);
        Assert.Equal("Nuevo Nombre", inDb!.Nombre);
        Assert.False(inDb.Activa);
    }

    [Fact]
    public async Task DeleteAsync_WithNoDependencies_DeletesPhysically()
    {
        using var context = CreateInMemoryDbContext(nameof(DeleteAsync_WithNoDependencies_DeletesPhysically));
        var service = new SucursalService(context, NullLogger<SucursalService>.Instance);

        var sucursal = new Sucursal { Nombre = "Sucursal Vacia", Activa = true };
        context.Sucursales.Add(sucursal);
        await context.SaveChangesAsync();

        var (success, deactivated, _) = await service.DeleteAsync(sucursal.Id);

        Assert.True(success);
        Assert.False(deactivated);
        Assert.Null(await context.Sucursales.FindAsync(sucursal.Id));
    }

    [Fact]
    public async Task DeleteAsync_WithUsers_DeactivatesSucursal()
    {
        using var context = CreateInMemoryDbContext(nameof(DeleteAsync_WithUsers_DeactivatesSucursal));
        var service = new SucursalService(context, NullLogger<SucursalService>.Instance);

        var sucursal = new Sucursal { Nombre = "Sucursal Con Empleado", Activa = true };
        context.Sucursales.Add(sucursal);
        await context.SaveChangesAsync();

        var usuario = new Usuario
        {
            Nombre = "Pepe",
            Apellido = "Test",
            Email = "pepe@test.local",
            Rol = "Empleado",
            SucursalId = sucursal.Id
        };
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();

        var (success, deactivated, message) = await service.DeleteAsync(sucursal.Id);

        Assert.True(success);
        Assert.True(deactivated);
        Assert.Contains("desactivada", message);

        var inDb = await context.Sucursales.FindAsync(sucursal.Id);
        Assert.NotNull(inDb);
        Assert.False(inDb.Activa);
    }
}
