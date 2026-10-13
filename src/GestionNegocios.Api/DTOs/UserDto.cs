namespace GestionNegocios.Api.DTOs;

/// <summary>
/// DTO con los datos públicos del usuario autenticado.
/// Nunca incluye campos sensibles como PasswordHash.
/// </summary>
public class UserDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public int? SucursalId { get; set; }
    public string? Avatar { get; set; }
}
