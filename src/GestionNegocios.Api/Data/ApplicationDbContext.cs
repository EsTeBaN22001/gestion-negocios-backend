using GestionNegocios.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionNegocios.Api.Data;

/// <summary>
/// Contexto principal de Entity Framework Core para el Sistema de Gestión de Negocios.
/// Contiene los DbSets y las configuraciones de relaciones según docs/05-database.md.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<DetalleVenta> DetallesVenta => Set<DetalleVenta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ─── Sucursal ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Sucursal>(entity =>
        {
            entity.ToTable("Sucursales");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Nombre).IsRequired().HasMaxLength(100);
            entity.Property(s => s.Direccion).HasMaxLength(200);
            entity.Property(s => s.Telefono).HasMaxLength(50);
            entity.Property(s => s.Activa).HasDefaultValue(true);
        });

        // ─── Usuario ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("Usuarios");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Nombre).IsRequired().HasMaxLength(100);
            entity.Property(u => u.Apellido).IsRequired().HasMaxLength(100);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(150);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(255);
            entity.Property(u => u.Rol).IsRequired().HasMaxLength(50);
            entity.Property(u => u.Avatar).HasMaxLength(255);
            entity.Property(u => u.Activo).HasDefaultValue(true);
            entity.Property(u => u.FechaCreacion).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            // Sucursal 1:N Usuario (SucursalId es nullable para Administrador)
            entity.HasOne(u => u.Sucursal)
                  .WithMany(s => s.Usuarios)
                  .HasForeignKey(u => u.SucursalId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── Categoria ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.ToTable("Categorias");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Nombre).IsRequired().HasMaxLength(100);
            entity.HasIndex(c => c.Nombre).IsUnique();
            entity.Property(c => c.Descripcion).HasMaxLength(500);
            entity.Property(c => c.Activa).HasDefaultValue(true);
        });

        // ─── Producto ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Producto>(entity =>
        {
            entity.ToTable("Productos");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Nombre).IsRequired().HasMaxLength(150);
            entity.Property(p => p.Codigo).IsRequired().HasMaxLength(50);
            entity.HasIndex(p => p.Codigo).IsUnique();
            entity.Property(p => p.Descripcion).HasMaxLength(500);
            entity.Property(p => p.Precio).HasPrecision(18, 2);
            entity.Property(p => p.Activo).HasDefaultValue(true);

            // Categoria 1:N Producto
            entity.HasOne(p => p.Categoria)
                  .WithMany(c => c.Productos)
                  .HasForeignKey(p => p.CategoriaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── Stock ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<Stock>(entity =>
        {
            entity.ToTable("Stocks");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Cantidad).HasDefaultValue(0);
            entity.Property(s => s.StockMinimo).HasDefaultValue(0);

            // UNIQUE(ProductoId, SucursalId) según docs/05-database.md
            entity.HasIndex(s => new { s.ProductoId, s.SucursalId }).IsUnique();

            // Producto 1:N Stock
            entity.HasOne(s => s.Producto)
                  .WithMany(p => p.Stocks)
                  .HasForeignKey(s => s.ProductoId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Sucursal 1:N Stock
            entity.HasOne(s => s.Sucursal)
                  .WithMany(suc => suc.Stocks)
                  .HasForeignKey(s => s.SucursalId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── Venta ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<Venta>(entity =>
        {
            entity.ToTable("Ventas");
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Fecha).HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
            entity.Property(v => v.Total).HasPrecision(18, 2);
            entity.Property(v => v.Estado).IsRequired().HasMaxLength(50).HasDefaultValue("Confirmada");
            entity.Property(v => v.ArchivoAdjunto).HasMaxLength(255);

            // Sucursal 1:N Venta
            entity.HasOne(v => v.Sucursal)
                  .WithMany(s => s.Ventas)
                  .HasForeignKey(v => v.SucursalId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Usuario 1:N Venta
            entity.HasOne(v => v.Usuario)
                  .WithMany(u => u.Ventas)
                  .HasForeignKey(v => v.UsuarioId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── DetalleVenta ──────────────────────────────────────────────────────
        modelBuilder.Entity<DetalleVenta>(entity =>
        {
            entity.ToTable("DetallesVenta");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Cantidad).IsRequired();
            entity.Property(d => d.PrecioUnitario).HasPrecision(18, 2);
            entity.Property(d => d.Subtotal).HasPrecision(18, 2);

            // Venta 1:N DetalleVenta (eliminación en cascada con la venta)
            entity.HasOne(d => d.Venta)
                  .WithMany(v => v.Detalles)
                  .HasForeignKey(d => d.VentaId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Producto 1:N DetalleVenta
            entity.HasOne(d => d.Producto)
                  .WithMany(p => p.DetallesVenta)
                  .HasForeignKey(d => d.ProductoId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
