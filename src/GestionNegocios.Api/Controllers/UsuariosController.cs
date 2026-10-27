using System.Security.Claims;
using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionNegocios.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;

    public UsuariosController(IUsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }

    private int GetCurrentUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idClaim, out var id) ? id : 0;
    }

    /// <summary>
    /// Obtiene una lista paginada de usuarios con soporte de filtros y búsqueda (Solo Administrador).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(PagedResultDto<UsuarioListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? rol = null,
        [FromQuery] int? sucursalId = null,
        [FromQuery] bool? soloActivos = null)
    {
        var result = await _usuarioService.GetPagedAsync(page, pageSize, search, rol, sucursalId, soloActivos);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene el detalle completo de un usuario por su identificador (Solo Administrador).
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(UsuarioDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(int id)
    {
        var usuario = await _usuarioService.GetByIdAsync(id);
        if (usuario == null) return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Usuario no encontrado" });

        return Ok(usuario);
    }

    /// <summary>
    /// Crea un nuevo usuario en el sistema (Solo Administrador).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(UsuarioDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] UsuarioCreateDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var (created, error) = await _usuarioService.CreateAsync(dto);
        if (error != null)
        {
            if (error.Contains("ya se encuentra registrado", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflicto",
                    Detail = error
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error de validación",
                Detail = error
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = created!.Id }, created);
    }

    /// <summary>
    /// Actualiza los datos de un usuario existente (Solo Administrador).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(UsuarioDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(int id, [FromBody] UsuarioUpdateDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var currentUserId = GetCurrentUserId();
        var (updated, error, notFound) = await _usuarioService.UpdateAsync(id, dto, currentUserId);

        if (notFound) return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Usuario no encontrado" });

        if (error != null)
        {
            if (error.Contains("ya está registrado", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflicto",
                    Detail = error
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Regla de negocio inválida",
                Detail = error
            });
        }

        return Ok(updated);
    }

    /// <summary>
    /// Alterna el estado activo/inactivo de un usuario (Solo Administrador).
    /// </summary>
    [HttpPatch("{id}/toggle-activo")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ToggleActivo(int id)
    {
        var currentUserId = GetCurrentUserId();
        var (success, error) = await _usuarioService.ToggleActivoAsync(id, currentUserId);

        if (!success)
        {
            if (error != null && error.Contains("no existe"))
            {
                return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Usuario no encontrado" });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Operación no permitida",
                Detail = error
            });
        }

        return Ok(new { message = "Estado de usuario actualizado correctamente." });
    }

    /// <summary>
    /// Sube o actualiza la foto de perfil (avatar) de un usuario.
    /// Administrador puede actualizar cualquier avatar; un usuario regular solo el suyo propio.
    /// </summary>
    [HttpPost("{id}/avatar")]
    [ProducesResponseType(typeof(UsuarioDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadAvatar(int id, IFormFile file)
    {
        var currentUserId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Administrador");

        if (!isAdmin && currentUserId != id)
        {
            return Forbid();
        }

        var (result, error, notFound) = await _usuarioService.ActualizarAvatarAsync(id, file);
        if (notFound)
        {
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Usuario no encontrado" });
        }

        if (error != null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error al subir avatar",
                Detail = error
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Elimina el avatar de un usuario y borra el archivo físico del disco.
    /// Administrador puede eliminar cualquier avatar; un usuario regular solo el suyo propio.
    /// </summary>
    [HttpDelete("{id}/avatar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAvatar(int id)
    {
        var currentUserId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Administrador");

        if (!isAdmin && currentUserId != id)
        {
            return Forbid();
        }

        var (success, error, notFound) = await _usuarioService.EliminarAvatarAsync(id);
        if (notFound)
        {
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Usuario no encontrado" });
        }

        return Ok(new { message = "Avatar eliminado correctamente." });
    }
}
