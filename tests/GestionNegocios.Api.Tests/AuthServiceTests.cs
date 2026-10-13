using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GestionNegocios.Api.Data;
using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Models;
using GestionNegocios.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace GestionNegocios.Api.Tests;

public class AuthServiceTests
{
    private const string TestJwtKey = "TestKeyMustBeAtLeast32BytesLong1234567890!";
    private const string TestIssuer = "GestionNegociosApi";
    private const string TestAudience = "GestionNegociosFrontend";

    private ApplicationDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private IConfiguration CreateTestConfiguration(int durationMinutes = 60)
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Jwt:Key", TestJwtKey },
            { "Jwt:Issuer", TestIssuer },
            { "Jwt:Audience", TestAudience },
            { "Jwt:DurationMinutes", durationMinutes.ToString() }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public async Task AuthenticateAsync_ValidCredentials_ReturnsTokenAndUserDtoWithoutPasswordHash()
    {
        // Arrange
        using var context = CreateInMemoryDbContext(nameof(AuthenticateAsync_ValidCredentials_ReturnsTokenAndUserDtoWithoutPasswordHash));
        var passwordHasher = new PasswordHasher<Usuario>();
        var config = CreateTestConfiguration();
        var service = new AuthService(context, config, passwordHasher, NullLogger<AuthService>.Instance);

        var usuario = new Usuario
        {
            Id = 1,
            Nombre = "Carlos",
            Apellido = "Gomez",
            Email = "carlos@test.local",
            Rol = "Administrador",
            Activo = true,
            SucursalId = null
        };
        usuario.PasswordHash = passwordHasher.HashPassword(usuario, "SecurePass123*");
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();

        var request = new LoginRequestDto
        {
            Email = "carlos@test.local",
            Password = "SecurePass123*"
        };

        // Act
        var result = await service.AuthenticateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
        Assert.NotNull(result.User);
        Assert.Equal(1, result.User.Id);
        Assert.Equal("Carlos", result.User.Nombre);
        Assert.Equal("Gomez", result.User.Apellido);
        Assert.Equal("carlos@test.local", result.User.Email);
        Assert.Equal("Administrador", result.User.Rol);
        Assert.Null(result.User.SucursalId);

        // Verificar que PasswordHash no está presente en el DTO
        var userDtoType = typeof(UserDto);
        Assert.Null(userDtoType.GetProperty("PasswordHash"));
    }

    [Fact]
    public async Task AuthenticateAsync_InvalidPassword_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryDbContext(nameof(AuthenticateAsync_InvalidPassword_ReturnsNull));
        var passwordHasher = new PasswordHasher<Usuario>();
        var config = CreateTestConfiguration();
        var service = new AuthService(context, config, passwordHasher, NullLogger<AuthService>.Instance);

        var usuario = new Usuario
        {
            Id = 2,
            Nombre = "Maria",
            Apellido = "Lopez",
            Email = "maria@test.local",
            Rol = "Empleado",
            Activo = true,
            SucursalId = 1
        };
        usuario.PasswordHash = passwordHasher.HashPassword(usuario, "CorrectPassword123*");
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();

        var request = new LoginRequestDto
        {
            Email = "maria@test.local",
            Password = "WrongPassword999*"
        };

        // Act
        var result = await service.AuthenticateAsync(request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AuthenticateAsync_NonExistentEmail_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryDbContext(nameof(AuthenticateAsync_NonExistentEmail_ReturnsNull));
        var passwordHasher = new PasswordHasher<Usuario>();
        var config = CreateTestConfiguration();
        var service = new AuthService(context, config, passwordHasher, NullLogger<AuthService>.Instance);

        var request = new LoginRequestDto
        {
            Email = "noexiste@test.local",
            Password = "SomePassword123*"
        };

        // Act
        var result = await service.AuthenticateAsync(request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AuthenticateAsync_InactiveUser_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryDbContext(nameof(AuthenticateAsync_InactiveUser_ReturnsNull));
        var passwordHasher = new PasswordHasher<Usuario>();
        var config = CreateTestConfiguration();
        var service = new AuthService(context, config, passwordHasher, NullLogger<AuthService>.Instance);

        var usuario = new Usuario
        {
            Id = 3,
            Nombre = "Inactivo",
            Apellido = "Usuario",
            Email = "inactivo@test.local",
            Rol = "Empleado",
            Activo = false, // INACTIVO
            SucursalId = 1
        };
        usuario.PasswordHash = passwordHasher.HashPassword(usuario, "Pass123*");
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();

        var request = new LoginRequestDto
        {
            Email = "inactivo@test.local",
            Password = "Pass123*"
        };

        // Act
        var result = await service.AuthenticateAsync(request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AuthenticateAsync_Empleado_IncludesSucursalIdInClaims()
    {
        // Arrange
        using var context = CreateInMemoryDbContext(nameof(AuthenticateAsync_Empleado_IncludesSucursalIdInClaims));
        var passwordHasher = new PasswordHasher<Usuario>();
        var config = CreateTestConfiguration();
        var service = new AuthService(context, config, passwordHasher, NullLogger<AuthService>.Instance);

        var usuario = new Usuario
        {
            Id = 10,
            Nombre = "Empleado",
            Apellido = "Prueba",
            Email = "empleado@test.local",
            Rol = "Empleado",
            Activo = true,
            SucursalId = 42
        };
        usuario.PasswordHash = passwordHasher.HashPassword(usuario, "Pass123*");
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();

        var request = new LoginRequestDto
        {
            Email = "empleado@test.local",
            Password = "Pass123*"
        };

        // Act
        var result = await service.AuthenticateAsync(request);

        // Assert
        Assert.NotNull(result);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(result.Token);

        var idClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "nameid" || c.Type == "sub");
        Assert.NotNull(idClaim);
        Assert.Equal("10", idClaim.Value);

        var roleClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role");
        Assert.NotNull(roleClaim);
        Assert.Equal("Empleado", roleClaim.Value);

        var sucursalClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "sucursalId");
        Assert.NotNull(sucursalClaim);
        Assert.Equal("42", sucursalClaim.Value);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ReturnsNullWhenInactiveOrNotFound()
    {
        // Arrange
        using var context = CreateInMemoryDbContext(nameof(GetCurrentUserAsync_ReturnsNullWhenInactiveOrNotFound));
        var passwordHasher = new PasswordHasher<Usuario>();
        var config = CreateTestConfiguration();
        var service = new AuthService(context, config, passwordHasher, NullLogger<AuthService>.Instance);

        var usuario = new Usuario
        {
            Id = 20,
            Nombre = "Desc",
            Apellido = "Activado",
            Email = "desactivado@test.local",
            Rol = "Empleado",
            Activo = false
        };
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();

        // Act & Assert
        var resultInactive = await service.GetCurrentUserAsync(20);
        Assert.Null(resultInactive);

        var resultNotFound = await service.GetCurrentUserAsync(999);
        Assert.Null(resultNotFound);
    }
}
