using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace GestionNegocios.Api.Tests;

public class JwtTokenValidationTests
{
    private const string SecretKey = "DevelopmentSecretKeyForTestingJwtValidation32CharsLong!";
    private const string Issuer = "GestionNegociosApi";
    private const string Audience = "GestionNegociosFrontend";

    private TokenValidationParameters GetValidationParameters(TimeSpan clockSkew)
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey)),
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            ClockSkew = clockSkew
        };
    }

    private string CreateToken(DateTime notBefore, DateTime expiresAt, string key = SecretKey, string iss = Issuer, string aud = Audience)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Email, "admin@test.local"),
                new Claim(ClaimTypes.Role, "Administrador")
            }),
            NotBefore = notBefore,
            IssuedAt = notBefore,
            Expires = expiresAt,
            Issuer = iss,
            Audience = aud,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(keyBytes),
                SecurityAlgorithms.HmacSha256)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    [Fact]
    public void ValidToken_SuccessfullyValidates()
    {
        // Arrange
        var tokenString = CreateToken(DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30));
        var handler = new JwtSecurityTokenHandler();
        var validationParameters = GetValidationParameters(TimeSpan.Zero);

        // Act
        var principal = handler.ValidateToken(tokenString, validationParameters, out var validatedToken);

        // Assert
        Assert.NotNull(principal);
        Assert.NotNull(validatedToken);
        Assert.Equal("1", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("Administrador", principal.FindFirst(ClaimTypes.Role)?.Value);
    }

    [Fact]
    public void ExpiredToken_ThrowsSecurityTokenExpiredException()
    {
        // Arrange: Token emitido hace 10 minutos y vencido hace 2 minutos
        var notBefore = DateTime.UtcNow.AddMinutes(-10);
        var expiresAt = DateTime.UtcNow.AddMinutes(-2);
        var tokenString = CreateToken(notBefore, expiresAt);

        var handler = new JwtSecurityTokenHandler();
        var validationParameters = GetValidationParameters(TimeSpan.Zero);

        // Act & Assert
        Assert.Throws<SecurityTokenExpiredException>(() =>
        {
            handler.ValidateToken(tokenString, validationParameters, out _);
        });
    }

    [Fact]
    public void TamperedTokenSignature_ThrowsSignatureVerificationException()
    {
        // Arrange: Generar token válido y alterar su firma
        var tokenString = CreateToken(DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30));
        var parts = tokenString.Split('.');
        Assert.Equal(3, parts.Length);

        // Alteramos la firma (tercera parte del JWT)
        var alteredSignature = parts[2] + "TAMPERED";
        var tamperedToken = $"{parts[0]}.{parts[1]}.{alteredSignature}";

        var handler = new JwtSecurityTokenHandler();
        var validationParameters = GetValidationParameters(TimeSpan.Zero);

        // Act & Assert
        Assert.ThrowsAny<SecurityTokenException>(() =>
        {
            handler.ValidateToken(tamperedToken, validationParameters, out _);
        });
    }

    [Fact]
    public void TokenSignedWithWrongKey_ThrowsSecurityTokenInvalidSignatureException()
    {
        // Arrange: Token firmado con otra clave
        const string wrongKey = "DifferentSecretKeyForSigningJwtMustBe32CharsLong!";
        var tokenWithWrongKey = CreateToken(DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30), key: wrongKey);

        var handler = new JwtSecurityTokenHandler();
        var validationParameters = GetValidationParameters(TimeSpan.Zero);

        // Act & Assert
        Assert.ThrowsAny<SecurityTokenException>(() =>
        {
            handler.ValidateToken(tokenWithWrongKey, validationParameters, out _);
        });
    }
}
