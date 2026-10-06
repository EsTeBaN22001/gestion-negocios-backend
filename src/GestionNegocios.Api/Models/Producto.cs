namespace GestionNegocios.Api.Models;

public class Producto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public bool Activo { get; set; } = true;
    public int CategoriaId { get; set; }

    // Relaciones
    public Categoria Categoria { get; set; } = null!;
    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
    public ICollection<DetalleVenta> DetallesVenta { get; set; } = new List<DetalleVenta>();
}
