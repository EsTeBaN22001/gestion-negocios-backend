using System.ComponentModel.DataAnnotations;

namespace GestionNegocios.Api.DTOs;

public class UsuarioListDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public int? SucursalId { get; set; }
    public string? SucursalNombre { get; set; }
    public bool Activo { get; set; }
    public string? Avatar { get; set; }
    public DateTime FechaCreacion { get; set; }
}

public class UsuarioDetailDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public int? SucursalId { get; set; }
    public string? SucursalNombre { get; set; }
    public bool Activo { get; set; }
    public string? Avatar { get; set; }
    public DateTime FechaCreacion { get; set; }
}

public class UsuarioCreateDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [StringLength(100, ErrorMessage = "El apellido no puede superar los 100 caracteres.")]
    public string Apellido { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El formato de correo electrónico no es válido.")]
    [StringLength(150, ErrorMessage = "El email no puede superar los 150 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "El rol es obligatorio.")]
    public string Rol { get; set; } = string.Empty; // "Administrador" o "Empleado"

    public int? SucursalId { get; set; }
}

public class UsuarioUpdateDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio.")]
    [StringLength(100, ErrorMessage = "El apellido no puede superar los 100 caracteres.")]
    public string Apellido { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "El formato de correo electrónico no es válido.")]
    [StringLength(150, ErrorMessage = "El email no puede superar los 150 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El rol es obligatorio.")]
    public string Rol { get; set; } = string.Empty;

    public int? SucursalId { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>
    /// Opcional: si se especifica, actualiza la contraseña del usuario.
    /// </summary>
    [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres si se modifica.")]
    public string? Password { get; set; }
}
