using System.ComponentModel.DataAnnotations;

namespace GestionNegocios.Api.DTOs;

/// <summary>
/// DTO para la solicitud de inicio de sesión.
/// </summary>
public class LoginRequestDto
{
    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    public string Password { get; set; } = string.Empty;
}
