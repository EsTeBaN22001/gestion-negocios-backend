using System.ComponentModel.DataAnnotations;

namespace GestionNegocios.Api.DTOs;

public class SucursalDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public bool Activa { get; set; }
    public int UsuariosCount { get; set; }
}

public class SucursalCreateDto
{
    [Required(ErrorMessage = "El nombre de la sucursal es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "La dirección no puede superar los 200 caracteres.")]
    public string? Direccion { get; set; }

    [StringLength(50, ErrorMessage = "El teléfono no puede superar los 50 caracteres.")]
    public string? Telefono { get; set; }
}

public class SucursalUpdateDto
{
    [Required(ErrorMessage = "El nombre de la sucursal es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "La dirección no puede superar los 200 caracteres.")]
    public string? Direccion { get; set; }

    [StringLength(50, ErrorMessage = "El teléfono no puede superar los 50 caracteres.")]
    public string? Telefono { get; set; }

    public bool Activa { get; set; } = true;
}
