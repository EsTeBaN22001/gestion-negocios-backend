namespace GestionNegocios.Api.Models;

public class Venta
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public decimal Total { get; set; }
    public int SucursalId { get; set; }
    public int UsuarioId { get; set; }
    public string Estado { get; set; } = "Confirmada"; // "Confirmada" o "Anulada"
    public string? ArchivoAdjunto { get; set; }

    // Relaciones
    public Sucursal Sucursal { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public ICollection<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();
}
