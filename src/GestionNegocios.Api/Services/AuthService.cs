using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GestionNegocios.Api.Data;
using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GestionNegocios.Api.Services;

/// <summary>
/// Implementación de la lógica de autenticación y generación de JWT.
/// Respeta el principio Controller -> Service -> DbContext con mapeo manual de DTOs.
/// </summary>
public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IPasswordHasher<Usuario> _passwordHasher;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        ApplicationDbContext context,
        IConfiguration configuration,
        IPasswordHasher<Usuario> passwordHasher,
        ILogger<AuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<LoginResponseDto?> AuthenticateAsync(LoginRequestDto request)
    {
        // 1. Buscar usuario por email (normalizado)
        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        var usuario = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Email.ToLower() == emailNormalized);

        if (usuario == null)
        {
            _logger.LogWarning("Intento de inicio de sesión fallido: usuario no encontrado.");
            return null;
        }

        // 2. Comprobar si el usuario está activo
        if (!usuario.Activo)
        {
            _logger.LogWarning("Intento de inicio de sesión fallido: cuenta desactivada para usuario ID {UserId}.", usuario.Id);
            return null;
        }

        // 3. Verificar la contraseña usando PasswordHasher<Usuario>
        var verificationResult = _passwordHasher.VerifyHashedPassword(
            usuario,
            usuario.PasswordHash,
            request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Intento de inicio de sesión fallido: contraseña inválida para usuario ID {UserId}.", usuario.Id);
            return null;
        }

        // Si el hash necesita actualización, se puede actualizar aquí si se desea
        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            usuario.PasswordHash = _passwordHasher.HashPassword(usuario, request.Password);
            await _context.SaveChangesAsync();
        }

        // 4. Generar token JWT
        var (tokenString, expiresAt) = GenerateJwtToken(usuario);

        // 5. Mapeo manual a DTO de respuesta (sin exponer PasswordHash)
        var response = new LoginResponseDto
        {
            Token = tokenString,
            ExpiresAt = expiresAt,
            User = MapToUserDto(usuario)
        };

        _logger.LogInformation("Inicio de sesión exitoso para usuario ID {UserId} ({Rol}).", usuario.Id, usuario.Rol);
        return response;
    }

    public async Task<UserDto?> GetCurrentUserAsync(int userId)
    {
        var usuario = await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.Activo);

        if (usuario == null)
        {
            return null;
        }

        return MapToUserDto(usuario);
    }

    private (string token, DateTime expiresAt) GenerateJwtToken(Usuario usuario)
    {
        var jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("La clave secreta 'Jwt:Key' no está configurada.");
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "GestionNegociosApi";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "GestionNegociosFrontend";
        var durationMinutes = int.TryParse(_configuration["Jwt:DurationMinutes"], out var parsedMinutes)
            ? parsedMinutes
            : 60;

        var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Email, usuario.Email),
            new(ClaimTypes.Name, $"{usuario.Nombre} {usuario.Apellido}".Trim()),
            new(ClaimTypes.Role, usuario.Rol),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (usuario.SucursalId.HasValue)
        {
            claims.Add(new Claim("sucursalId", usuario.SucursalId.Value.ToString()));
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(durationMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = jwtIssuer,
            Audience = jwtAudience,
            SigningCredentials = signingCredentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return (tokenString, expiresAt);
    }

    private static UserDto MapToUserDto(Usuario usuario)
    {
        return new UserDto
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Apellido = usuario.Apellido,
            Email = usuario.Email,
            Rol = usuario.Rol,
            SucursalId = usuario.SucursalId,
            Avatar = usuario.Avatar
        };
    }
}
