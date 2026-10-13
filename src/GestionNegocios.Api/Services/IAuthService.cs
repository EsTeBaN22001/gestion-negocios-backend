using GestionNegocios.Api.DTOs;

namespace GestionNegocios.Api.Services;

/// <summary>
/// Contrato del servicio de autenticación.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Valida credenciales, comprueba estado activo y genera el token JWT.
    /// Retorna null si las credenciales son inválidas o la cuenta está inactiva.
    /// </summary>
    Task<LoginResponseDto?> AuthenticateAsync(LoginRequestDto request);

    /// <summary>
    /// Obtiene los datos del usuario autenticado por su ID.
    /// </summary>
    Task<UserDto?> GetCurrentUserAsync(int userId);
}
