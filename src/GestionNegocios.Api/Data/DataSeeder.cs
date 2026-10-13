using GestionNegocios.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GestionNegocios.Api.Data;

/// <summary>
/// Sembrador de datos iniciales mínimos para desarrollo y pruebas.
/// Cumple con docs/05-database.md (sección Seed inicial) y las reglas de negocio de docs/02-business-rules.md.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DataSeeder");

        try
        {
            // 1. Sucursal inicial si no existe ninguna (requerida para los empleados)
            Sucursal? sucursalCentral = await context.Sucursales.OrderBy(s => s.Id).FirstOrDefaultAsync();
            if (sucursalCentral == null)
            {
                sucursalCentral = new Sucursal
                {
                    Nombre = "Sucursal Central",
                    Direccion = "Av. Corrientes 1234",
                    Telefono = "011-4567-8900",
                    Activa = true
                };
                context.Sucursales.Add(sucursalCentral);
                await context.SaveChangesAsync();
                logger.LogInformation("Se creó la sucursal inicial 'Sucursal Central' (ID: {SucursalId}).", sucursalCentral.Id);
            }

            // 2. Usuarios iniciales si la tabla Usuarios está vacía
            if (!await context.Usuarios.AnyAsync())
            {
                var adminEmail = configuration["Seed:AdminEmail"] ?? "admin@gestionnegocios.local";
                var adminPassword = configuration["Seed:AdminPassword"] ?? "Admin123*";

                var employeeEmail = configuration["Seed:EmployeeEmail"] ?? "empleado@gestionnegocios.local";
                var employeePassword = configuration["Seed:EmployeePassword"] ?? "Empleado123*";

                // Administrador (SucursalId = null según regla 4 de docs/02-business-rules.md)
                var admin = new Usuario
                {
                    Nombre = "Administrador",
                    Apellido = "Sistema",
                    Email = adminEmail.Trim().ToLowerInvariant(),
                    Rol = "Administrador",
                    SucursalId = null,
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow
                };
                admin.PasswordHash = passwordHasher.HashPassword(admin, adminPassword);

                // Empleado (SucursalId asignado según regla 3 de docs/02-business-rules.md)
                var empleado = new Usuario
                {
                    Nombre = "Juan",
                    Apellido = "Pérez",
                    Email = employeeEmail.Trim().ToLowerInvariant(),
                    Rol = "Empleado",
                    SucursalId = sucursalCentral.Id,
                    Activo = true,
                    FechaCreacion = DateTime.UtcNow
                };
                empleado.PasswordHash = passwordHasher.HashPassword(empleado, employeePassword);

                context.Usuarios.AddRange(admin, empleado);
                await context.SaveChangesAsync();

                logger.LogInformation("Usuarios de desarrollo inicializados exitosamente ({AdminEmail}, {EmployeeEmail}).", adminEmail, employeeEmail);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error durante la inicialización de datos de prueba.");
        }
    }
}
