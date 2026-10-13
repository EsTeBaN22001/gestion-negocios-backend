namespace GestionNegocios.Api.DTOs;

/// <summary>
/// DTO con la respuesta exitosa del inicio de sesión.
/// Contiene el token JWT, su fecha de expiración y los datos públicos del usuario.
/// </summary>
public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
}
