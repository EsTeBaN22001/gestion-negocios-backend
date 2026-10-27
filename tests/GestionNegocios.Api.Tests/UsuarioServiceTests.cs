using GestionNegocios.Api.Data;
using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Models;
using GestionNegocios.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestionNegocios.Api.Tests;

public class UsuarioServiceTests
{
    private ApplicationDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_DuplicateEmail_ReturnsError()
    {
        using var context = CreateInMemoryDbContext(nameof(CreateAsync_DuplicateEmail_ReturnsError));
        var hasher = new PasswordHasher<Usuario>();
        var service = new UsuarioService(context, hasher, NullLogger<UsuarioService>.Instance, null!);

        context.Usuarios.Add(new Usuario
        {
            Nombre = "Juan",
            Apellido = "Perez",
            Email = "juan@test.local",
            Rol = "Administrador"
        });
        await context.SaveChangesAsync();

        var (created, error) = await service.CreateAsync(new UsuarioCreateDto
        {
            Nombre = "Otro",
            Apellido = "User",
            Email = "juan@test.local",
            Password = "Password123*",
            Rol = "Administrador"
        });

        Assert.Null(created);
        Assert.NotNull(error);
        Assert.Contains("ya se encuentra registrado", error);
    }

    [Fact]
    public async Task CreateAsync_EmpleadoWithoutSucursal_ReturnsError()
    {
        using var context = CreateInMemoryDbContext(nameof(CreateAsync_EmpleadoWithoutSucursal_ReturnsError));
        var hasher = new PasswordHasher<Usuario>();
        var service = new UsuarioService(context, hasher, NullLogger<UsuarioService>.Instance, null!);

        var (created, error) = await service.CreateAsync(new UsuarioCreateDto
        {
            Nombre = "Carlos",
            Apellido = "Gomez",
            Email = "carlos@test.local",
            Password = "Password123*",
            Rol = "Empleado",
            SucursalId = null // Inválido para Empleado
        });

        Assert.Null(created);
        Assert.NotNull(error);
        Assert.Contains("debe pertenecer a una sucursal", error);
    }

    [Fact]
    public async Task CreateAsync_EmpleadoWithValidSucursal_CreatesAndHashesPassword()
    {
        using var context = CreateInMemoryDbContext(nameof(CreateAsync_EmpleadoWithValidSucursal_CreatesAndHashesPassword));
        var hasher = new PasswordHasher<Usuario>();
        var service = new UsuarioService(context, hasher, NullLogger<UsuarioService>.Instance, null!);

        var sucursal = new Sucursal { Nombre = "Sucursal Norte", Activa = true };
        context.Sucursales.Add(sucursal);
        await context.SaveChangesAsync();

        var (created, error) = await service.CreateAsync(new UsuarioCreateDto
        {
            Nombre = "Lucas",
            Apellido = "Diaz",
            Email = "lucas@test.local",
            Password = "SuperPassword123*",
            Rol = "Empleado",
            SucursalId = sucursal.Id
        });

        Assert.Null(error);
        Assert.NotNull(created);
        Assert.Equal("Lucas", created.Nombre);
        Assert.Equal("Sucursal Norte", created.SucursalNombre);

        var inDb = await context.Usuarios.FindAsync(created.Id);
        Assert.NotNull(inDb);
        Assert.NotEqual("SuperPassword123*", inDb.PasswordHash);
        var verification = hasher.VerifyHashedPassword(inDb, inDb.PasswordHash, "SuperPassword123*");
        Assert.Equal(PasswordVerificationResult.Success, verification);
    }

    [Fact]
    public async Task CreateAsync_AdministradorWithoutSucursal_Allowed()
    {
        using var context = CreateInMemoryDbContext(nameof(CreateAsync_AdministradorWithoutSucursal_Allowed));
        var hasher = new PasswordHasher<Usuario>();
        var service = new UsuarioService(context, hasher, NullLogger<UsuarioService>.Instance, null!);

        var (created, error) = await service.CreateAsync(new UsuarioCreateDto
        {
            Nombre = "Super",
            Apellido = "Admin",
            Email = "admin2@test.local",
            Password = "SuperPassword123*",
            Rol = "Administrador",
            SucursalId = null
        });

        Assert.Null(error);
        Assert.NotNull(created);
        Assert.Equal("Administrador", created.Rol);
        Assert.Null(created.SucursalId);
    }

    [Fact]
    public async Task UpdateAsync_AdminDeactivatingSelf_ReturnsError()
    {
        using var context = CreateInMemoryDbContext(nameof(UpdateAsync_AdminDeactivatingSelf_ReturnsError));
        var hasher = new PasswordHasher<Usuario>();
        var service = new UsuarioService(context, hasher, NullLogger<UsuarioService>.Instance, null!);

        var admin = new Usuario
        {
            Nombre = "Admin",
            Apellido = "Actual",
            Email = "admin@test.local",
            Rol = "Administrador",
            Activo = true
        };
        context.Usuarios.Add(admin);
        await context.SaveChangesAsync();

        var (updated, error, _) = await service.UpdateAsync(admin.Id, new UsuarioUpdateDto
        {
            Nombre = "Admin",
            Apellido = "Actual",
            Email = "admin@test.local",
            Rol = "Administrador",
            Activo = false // Intentar desactivarse a sí mismo
        }, currentUserId: admin.Id);

        Assert.Null(updated);
        Assert.NotNull(error);
        Assert.Contains("No puede desactivar su propia cuenta", error);
    }

    [Fact]
    public async Task ToggleActivoAsync_OnlyAdmin_CannotDeactivate()
    {
        using var context = CreateInMemoryDbContext(nameof(ToggleActivoAsync_OnlyAdmin_CannotDeactivate));
        var hasher = new PasswordHasher<Usuario>();
        var service = new UsuarioService(context, hasher, NullLogger<UsuarioService>.Instance, null!);

        var admin = new Usuario
        {
            Nombre = "Admin",
            Apellido = "Unico",
            Email = "admin_unico@test.local",
            Rol = "Administrador",
            Activo = true
        };
        var otroUsuario = new Usuario
        {
            Nombre = "Empleado",
            Apellido = "Test",
            Email = "empleado@test.local",
            Rol = "Empleado",
            Activo = true
        };
        context.Usuarios.AddRange(admin, otroUsuario);
        await context.SaveChangesAsync();

        // Intento de desactivar al único admin
        var (success, error) = await service.ToggleActivoAsync(admin.Id, currentUserId: 999);

        Assert.False(success);
        Assert.Contains("único Administrador activo", error);
    }

    [Fact]
    public async Task ActualizarAvatarAsync_ValidFile_SavesAndDeletesOldAvatar()
    {
        using var context = CreateInMemoryDbContext(nameof(ActualizarAvatarAsync_ValidFile_SavesAndDeletesOldAvatar));
        var hasher = new PasswordHasher<Usuario>();
        var fakeStorage = new FakeFileStorageService
        {
            RelativePathToReturn = "/uploads/avatars/new.png"
        };

        var service = new UsuarioService(context, hasher, NullLogger<UsuarioService>.Instance, fakeStorage);

        var usuario = new Usuario
        {
            Nombre = "Ana",
            Apellido = "Gomez",
            Email = "ana@test.local",
            Rol = "Administrador",
            Avatar = "/uploads/avatars/old.png"
        };
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();

        var formFile = new Microsoft.AspNetCore.Http.FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "avatar.png")
        {
            Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
            ContentType = "image/png"
        };
        var (result, error, notFound) = await service.ActualizarAvatarAsync(usuario.Id, formFile);

        Assert.False(notFound);
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("/uploads/avatars/new.png", result.Avatar);

        // Verifica que se haya ordenado eliminar el avatar viejo
        Assert.Contains("/uploads/avatars/old.png", fakeStorage.DeletedFiles);
    }

    [Fact]
    public async Task EliminarAvatarAsync_ExistingAvatar_DeletesAndClearsProperty()
    {
        using var context = CreateInMemoryDbContext(nameof(EliminarAvatarAsync_ExistingAvatar_DeletesAndClearsProperty));
        var hasher = new PasswordHasher<Usuario>();
        var fakeStorage = new FakeFileStorageService();
        var service = new UsuarioService(context, hasher, NullLogger<UsuarioService>.Instance, fakeStorage);

        var usuario = new Usuario
        {
            Nombre = "Pedro",
            Apellido = "Lopez",
            Email = "pedro@test.local",
            Rol = "Empleado",
            Avatar = "/uploads/avatars/pedro.jpg"
        };
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();

        var (success, error, notFound) = await service.EliminarAvatarAsync(usuario.Id);

        Assert.True(success);
        Assert.Null(error);
        Assert.False(notFound);
        Assert.Contains("/uploads/avatars/pedro.jpg", fakeStorage.DeletedFiles);

        var reloaded = await context.Usuarios.FindAsync(usuario.Id);
        Assert.Null(reloaded!.Avatar);
    }
}
