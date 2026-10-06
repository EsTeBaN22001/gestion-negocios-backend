namespace GestionNegocios.Api.Models;

public class Sucursal
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public bool Activa { get; set; } = true;

    // Relaciones
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
}
