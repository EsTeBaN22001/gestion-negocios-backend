using GestionNegocios.Api.Data;
using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GestionNegocios.Api.Services;

public class UsuarioService : IUsuarioService
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher<Usuario> _passwordHasher;
    private readonly ILogger<UsuarioService> _logger;

    public UsuarioService(
        ApplicationDbContext context,
        IPasswordHasher<Usuario> passwordHasher,
        ILogger<UsuarioService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<PagedResultDto<UsuarioListDto>> GetPagedAsync(
        int page = 1,
        int pageSize = 10,
        string? search = null,
        string? rol = null,
        int? sucursalId = null,
        bool? soloActivos = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Usuarios
            .AsNoTracking()
            .Include(u => u.Sucursal)
            .AsQueryable();

        if (soloActivos.HasValue)
        {
            query = query.Where(u => u.Activo == soloActivos.Value);
        }

        if (!string.IsNullOrWhiteSpace(rol))
        {
            var rolNormalized = rol.Trim();
            query = query.Where(u => u.Rol == rolNormalized);
        }

        if (sucursalId.HasValue)
        {
            query = query.Where(u => u.SucursalId == sucursalId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u => u.Nombre.ToLower().Contains(term) ||
                                     u.Apellido.ToLower().Contains(term) ||
                                     u.Email.ToLower().Contains(term) ||
                                     (u.Sucursal != null && u.Sucursal.Nombre.ToLower().Contains(term)));
        }

        var totalItems = await query.CountAsync();

        var items = await query
            .OrderBy(u => u.Apellido).ThenBy(u => u.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UsuarioListDto
            {
                Id = u.Id,
                Nombre = u.Nombre,
                Apellido = u.Apellido,
                Email = u.Email,
                Rol = u.Rol,
                SucursalId = u.SucursalId,
                SucursalNombre = u.Sucursal != null ? u.Sucursal.Nombre : null,
                Activo = u.Activo,
                Avatar = u.Avatar,
                FechaCreacion = u.FechaCreacion
            })
            .ToListAsync();

        return new PagedResultDto<UsuarioListDto>(items, page, pageSize, totalItems);
    }

    public async Task<UsuarioDetailDto?> GetByIdAsync(int id)
    {
        var usuario = await _context.Usuarios
            .AsNoTracking()
            .Include(u => u.Sucursal)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (usuario == null) return null;

        return new UsuarioDetailDto
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Apellido = usuario.Apellido,
            Email = usuario.Email,
            Rol = usuario.Rol,
            SucursalId = usuario.SucursalId,
            SucursalNombre = usuario.Sucursal != null ? usuario.Sucursal.Nombre : null,
            Activo = usuario.Activo,
            Avatar = usuario.Avatar,
            FechaCreacion = usuario.FechaCreacion
        };
    }

    public async Task<(UsuarioDetailDto? result, string? error)> CreateAsync(UsuarioCreateDto dto)
    {
        var emailNormalized = dto.Email.Trim().ToLowerInvariant();

        // 1. Validar unicidad del Email
        var emailExists = await _context.Usuarios
            .AnyAsync(u => u.Email.ToLower() == emailNormalized);

        if (emailExists)
        {
            return (null, "El correo electrónico ya se encuentra registrado.");
        }

        // 2. Validar rol válido
        var rol = dto.Rol.Trim();
        if (rol != "Administrador" && rol != "Empleado")
        {
            return (null, "El rol especificado no es válido. Los roles válidos son 'Administrador' y 'Empleado'.");
        }

        // 3. Validar sucursal según rol (docs/02-business-rules.md: regla 3 y 4)
        if (rol == "Empleado")
        {
            if (!dto.SucursalId.HasValue)
            {
                return (null, "Un usuario con rol Empleado debe pertenecer a una sucursal.");
            }

            var sucursalExiste = await _context.Sucursales.AnyAsync(s => s.Id == dto.SucursalId.Value && s.Activa);
            if (!sucursalExiste)
            {
                return (null, "La sucursal especificada no existe o no se encuentra activa.");
            }
        }
        else // Administrador
        {
            if (dto.SucursalId.HasValue)
            {
                var sucursalExiste = await _context.Sucursales.AnyAsync(s => s.Id == dto.SucursalId.Value);
                if (!sucursalExiste)
                {
                    return (null, "La sucursal especificada no existe.");
                }
            }
        }

        // 4. Crear entidad y hashear contraseña
        var usuario = new Usuario
        {
            Nombre = dto.Nombre.Trim(),
            Apellido = dto.Apellido.Trim(),
            Email = emailNormalized,
            Rol = rol,
            SucursalId = rol == "Empleado" ? dto.SucursalId : dto.SucursalId,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        usuario.PasswordHash = _passwordHasher.HashPassword(usuario, dto.Password);

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Usuario creado con éxito: {Email} (ID: {Id}, Rol: {Rol})", usuario.Email, usuario.Id, usuario.Rol);

        // Cargar nombre de sucursal si tiene
        string? sucursalNombre = null;
        if (usuario.SucursalId.HasValue)
        {
            sucursalNombre = await _context.Sucursales
                .Where(s => s.Id == usuario.SucursalId.Value)
                .Select(s => s.Nombre)
                .FirstOrDefaultAsync();
        }

        return (new UsuarioDetailDto
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Apellido = usuario.Apellido,
            Email = usuario.Email,
            Rol = usuario.Rol,
            SucursalId = usuario.SucursalId,
            SucursalNombre = sucursalNombre,
            Activo = usuario.Activo,
            Avatar = usuario.Avatar,
            FechaCreacion = usuario.FechaCreacion
        }, null);
    }

    public async Task<(UsuarioDetailDto? result, string? error, bool notFound)> UpdateAsync(int id, UsuarioUpdateDto dto, int currentUserId)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Sucursal)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (usuario == null)
        {
            return (null, null, true);
        }

        var emailNormalized = dto.Email.Trim().ToLowerInvariant();

        // 1. Validar duplicado de email
        var duplicateEmail = await _context.Usuarios
            .AnyAsync(u => u.Id != id && u.Email.ToLower() == emailNormalized);

        if (duplicateEmail)
        {
            return (null, "El correo electrónico ya está registrado por otro usuario.", false);
        }

        // 2. Validar rol válido
        var rol = dto.Rol.Trim();
        if (rol != "Administrador" && rol != "Empleado")
        {
            return (null, "El rol especificado no es válido. Los roles permitidos son 'Administrador' y 'Empleado'.", false);
        }

        // 3. Reglas de auto-protección del administrador
        if (id == currentUserId)
        {
            if (usuario.Rol == "Administrador" && rol != "Administrador")
            {
                return (null, "No puede cambiar su propio rol de Administrador.", false);
            }

            if (!dto.Activo)
            {
                return (null, "No puede desactivar su propia cuenta de usuario.", false);
            }
        }

        // Si se intenta desactivar un administrador, verificar que no sea el único activo
        if (usuario.Rol == "Administrador" && (!dto.Activo || rol != "Administrador"))
        {
            var adminActivos = await _context.Usuarios
                .CountAsync(u => u.Rol == "Administrador" && u.Activo && u.Id != id);

            if (adminActivos == 0)
            {
                return (null, "No se puede desactivar ni cambiar el rol del único Administrador activo del sistema.", false);
            }
        }

        // 4. Validar sucursal según rol
        if (rol == "Empleado")
        {
            if (!dto.SucursalId.HasValue)
            {
                return (null, "Un usuario con rol Empleado debe pertenecer a una sucursal.", false);
            }

            var sucursalExiste = await _context.Sucursales.AnyAsync(s => s.Id == dto.SucursalId.Value && s.Activa);
            if (!sucursalExiste)
            {
                return (null, "La sucursal especificada no existe o se encuentra inactiva.", false);
            }
        }

        // 5. Aplicar cambios
        usuario.Nombre = dto.Nombre.Trim();
        usuario.Apellido = dto.Apellido.Trim();
        usuario.Email = emailNormalized;
        usuario.Rol = rol;
        usuario.SucursalId = rol == "Empleado" ? dto.SucursalId : dto.SucursalId;
        usuario.Activo = dto.Activo;

        // Actualizar contraseña si se envió una nueva
        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            usuario.PasswordHash = _passwordHasher.HashPassword(usuario, dto.Password);
            _logger.LogInformation("Contraseña actualizada para usuario ID {Id}", id);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Usuario ID {Id} actualizado con éxito.", id);

        string? sucursalNombre = null;
        if (usuario.SucursalId.HasValue)
        {
            sucursalNombre = await _context.Sucursales
                .Where(s => s.Id == usuario.SucursalId.Value)
                .Select(s => s.Nombre)
                .FirstOrDefaultAsync();
        }

        return (new UsuarioDetailDto
        {
            Id = usuario.Id,
            Nombre = usuario.Nombre,
            Apellido = usuario.Apellido,
            Email = usuario.Email,
            Rol = usuario.Rol,
            SucursalId = usuario.SucursalId,
            SucursalNombre = sucursalNombre,
            Activo = usuario.Activo,
            Avatar = usuario.Avatar,
            FechaCreacion = usuario.FechaCreacion
        }, null, false);
    }

    public async Task<(bool success, string? error)> ToggleActivoAsync(int id, int currentUserId)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
        {
            return (false, "El usuario no existe.");
        }

        if (id == currentUserId && usuario.Activo)
        {
            return (false, "No puede desactivar su propia cuenta.");
        }

        if (usuario.Rol == "Administrador" && usuario.Activo)
        {
            var adminActivos = await _context.Usuarios
                .CountAsync(u => u.Rol == "Administrador" && u.Activo && u.Id != id);

            if (adminActivos == 0)
            {
                return (false, "No se puede desactivar al único Administrador activo del sistema.");
            }
        }

        usuario.Activo = !usuario.Activo;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Estado del usuario ID {Id} cambiado a Activo={Activo}", id, usuario.Activo);
        return (true, null);
    }
}
