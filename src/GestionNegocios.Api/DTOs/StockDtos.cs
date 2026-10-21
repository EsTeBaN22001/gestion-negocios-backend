using System.ComponentModel.DataAnnotations;

namespace GestionNegocios.Api.DTOs;

public class StockListDto
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public string ProductoCodigo { get; set; } = string.Empty;
    public string ProductoNombre { get; set; } = string.Empty;
    public decimal ProductoPrecio { get; set; }
    public bool ProductoActivo { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public int SucursalId { get; set; }
    public string SucursalNombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public int StockMinimo { get; set; }

    /// <summary>
    /// Estado del stock: "Agotado" (Cantidad == 0), "Bajo" (Cantidad <= StockMinimo), o "Normal".
    /// </summary>
    public string AlertaStock
    {
        get
        {
            if (Cantidad == 0) return "Agotado";
            if (Cantidad <= StockMinimo) return "Bajo";
            return "Normal";
        }
    }
}

public class StockDetailDto
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public string ProductoCodigo { get; set; } = string.Empty;
    public string ProductoNombre { get; set; } = string.Empty;
    public decimal ProductoPrecio { get; set; }
    public bool ProductoActivo { get; set; }
    public string CategoriaNombre { get; set; } = string.Empty;
    public int SucursalId { get; set; }
    public string SucursalNombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public int StockMinimo { get; set; }
    public string AlertaStock
    {
        get
        {
            if (Cantidad == 0) return "Agotado";
            if (Cantidad <= StockMinimo) return "Bajo";
            return "Normal";
        }
    }
}

public class StockCreateDto
{
    [Required(ErrorMessage = "El producto es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "Debe especificar un producto válido.")]
    public int ProductoId { get; set; }

    [Required(ErrorMessage = "La sucursal es obligatoria.")]
    [Range(1, int.MaxValue, ErrorMessage = "Debe especificar una sucursal válida.")]
    public int SucursalId { get; set; }

    [Range(0, 1000000, ErrorMessage = "La cantidad inicial no puede ser negativa.")]
    public int Cantidad { get; set; } = 0;

    [Range(0, 1000000, ErrorMessage = "El stock mínimo no puede ser negativo.")]
    public int StockMinimo { get; set; } = 5;
}

public class StockAjusteDto
{
    [Required(ErrorMessage = "La cantidad es obligatoria.")]
    [Range(0, 1000000, ErrorMessage = "La cantidad no puede ser negativa.")]
    public int Cantidad { get; set; }

    [Required(ErrorMessage = "El stock mínimo es obligatorio.")]
    [Range(0, 1000000, ErrorMessage = "El stock mínimo no puede ser negativo.")]
    public int StockMinimo { get; set; }

    [StringLength(250, ErrorMessage = "El motivo del ajuste no puede superar los 250 caracteres.")]
    public string? Motivo { get; set; }
}
