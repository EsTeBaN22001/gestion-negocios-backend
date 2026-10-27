using System.Security.Claims;
using GestionNegocios.Api.Controllers;
using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestionNegocios.Api.Tests;

public class AuthControllerTests
{
    private class FakeAuthService : IAuthService
    {
        public Task<LoginResponseDto?> AuthenticateAsync(LoginRequestDto request)
        {
            if (request.Email == "admin@test.local" && request.Password == "Valid123*")
            {
                return Task.FromResult<LoginResponseDto?>(new LoginResponseDto
                {
                    Token = "fake-jwt-token",
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    User = new UserDto
                    {
                        Id = 1,
                        Nombre = "Admin",
                        Apellido = "User",
                        Email = "admin@test.local",
                        Rol = "Administrador",
                        SucursalId = null
                    }
                });
            }

            return Task.FromResult<LoginResponseDto?>(null);
        }

        public Task<UserDto?> GetCurrentUserAsync(int userId)
        {
            if (userId == 1)
            {
                return Task.FromResult<UserDto?>(new UserDto
                {
                    Id = 1,
                    Nombre = "Admin",
                    Apellido = "User",
                    Email = "admin@test.local",
                    Rol = "Administrador",
                    SucursalId = null
                });
            }

            return Task.FromResult<UserDto?>(null);
        }
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkWithResponseDto()
    {
        // Arrange
        var controller = new AuthController(new FakeAuthService(), null!, NullLogger<AuthController>.Instance);
        var request = new LoginRequestDto
        {
            Email = "admin@test.local",
            Password = "Valid123*"
        };

        // Act
        var result = await controller.Login(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LoginResponseDto>(okResult.Value);
        Assert.Equal("fake-jwt-token", response.Token);
        Assert.Equal("admin@test.local", response.User.Email);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorizedWithProblemDetails()
    {
        // Arrange
        var controller = new AuthController(new FakeAuthService(), null!, NullLogger<AuthController>.Instance);
        var request = new LoginRequestDto
        {
            Email = "admin@test.local",
            Password = "WrongPassword*"
        };

        // Act
        var result = await controller.Login(request);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal(StatusCodes.Status401Unauthorized, problemDetails.Status);
        // Comprobar mensaje genérico que no revela si falló el email o la contraseña
        Assert.Contains("El email o la contraseña son incorrectos", problemDetails.Detail);
    }

    [Fact]
    public async Task GetCurrentUser_ValidClaims_ReturnsOkWithUserDto()
    {
        // Arrange
        var controller = new AuthController(new FakeAuthService(), null!, NullLogger<AuthController>.Instance);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "1"),
            new(ClaimTypes.Email, "admin@test.local"),
            new(ClaimTypes.Role, "Administrador")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        // Act
        var result = await controller.GetCurrentUser();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var userDto = Assert.IsType<UserDto>(okResult.Value);
        Assert.Equal(1, userDto.Id);
        Assert.Equal("Admin", userDto.Nombre);
    }

    [Fact]
    public async Task GetCurrentUser_InvalidClaims_ReturnsUnauthorized()
    {
        // Arrange
        var controller = new AuthController(new FakeAuthService(), null!, NullLogger<AuthController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        // Act
        var result = await controller.GetCurrentUser();

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal(StatusCodes.Status401Unauthorized, problemDetails.Status);
    }

    [Fact]
    public void AdminCheck_AsAdmin_ReturnsOk()
    {
        // Arrange
        var controller = new AuthController(new FakeAuthService(), null!, NullLogger<AuthController>.Instance);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "1"),
            new(ClaimTypes.Email, "admin@test.local"),
            new(ClaimTypes.Role, "Administrador")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        // Act
        var result = controller.AdminCheck();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }
}
