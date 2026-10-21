using System.ComponentModel.DataAnnotations;

namespace GestionNegocios.Api.DTOs;

public class VentaDetalleCreateDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Debe especificar un producto válido.")]
    public int ProductoId { get; set; }

    [Range(1, 1000000, ErrorMessage = "La cantidad debe ser mayor a cero.")]
    public int Cantidad { get; set; }
}

public class VentaCreateDto
{
    // Para Empleados este valor se ignora: se usa su sucursal del JWT.
    [Range(1, int.MaxValue, ErrorMessage = "Debe especificar una sucursal válida.")]
    public int? SucursalId { get; set; }

    [Required(ErrorMessage = "La venta debe incluir al menos un detalle.")]
    [MinLength(1, ErrorMessage = "La venta debe incluir al menos un detalle.")]
    public List<VentaDetalleCreateDto> Detalles { get; set; } = [];
}

public class VentaListDto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int SucursalId { get; set; }
    public string SucursalNombre { get; set; } = string.Empty;
    public int UsuarioId { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public int DetallesCount { get; set; }
}

public class VentaDetalleDto
{
    public int ProductoId { get; set; }
    public string ProductoCodigo { get; set; } = string.Empty;
    public string ProductoNombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
}

public class VentaDetailDto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int SucursalId { get; set; }
    public string SucursalNombre { get; set; } = string.Empty;
    public int UsuarioId { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public List<VentaDetalleDto> Detalles { get; set; } = [];
}
