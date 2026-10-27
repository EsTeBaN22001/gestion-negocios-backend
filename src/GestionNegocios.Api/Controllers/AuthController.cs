using System.Security.Claims;
using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionNegocios.Api.Controllers;

/// <summary>
/// Controlador de autenticación y autorización mediante JWT.
/// Respeta las convenciones de docs/06-api.md y docs/08-authentication.md.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUsuarioService _usuarioService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IUsuarioService usuarioService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _usuarioService = usuarioService;
        _logger = logger;
    }

    /// <summary>
    /// Inicia sesión y genera un token JWT firmado.
    /// </summary>
    /// <param name="request">Credenciales de acceso (email y contraseña).</param>
    /// <returns>Token JWT con vencimiento y datos del usuario, o 401 si las credenciales son incorrectas.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _authService.AuthenticateAsync(request);
        if (result == null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Credenciales incorrectas",
                Detail = "El email o la contraseña son incorrectos o la cuenta se encuentra desactivada."
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Obtiene la información del usuario autenticado actual a partir del token JWT.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Token inválido",
                Detail = "No se pudo identificar al usuario en el token proporcionado."
            });
        }

        var user = await _authService.GetCurrentUserAsync(userId);
        if (user == null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Usuario no disponible",
                Detail = "El usuario no existe o se encuentra inactivo."
            });
        }

        return Ok(user);
    }

    /// <summary>
    /// Endpoint de verificación de autorización restringido exclusivamente al rol Administrador.
    /// </summary>
    [HttpGet("admin-check")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult AdminCheck()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);
        var rol = User.FindFirstValue(ClaimTypes.Role);

        return Ok(new
        {
            message = "Acceso concedido exitosamente a funcionalidad administrativa.",
            userId,
            email,
            rol,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Sube o actualiza la foto de perfil (avatar) del usuario actualmente autenticado.
    /// </summary>
    [HttpPost("perfil/avatar")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadMyAvatar(IFormFile file)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var (updatedUser, error, notFound) = await _usuarioService.ActualizarAvatarAsync(userId, file);
        if (notFound) return Unauthorized();

        if (error != null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error al subir avatar",
                Detail = error
            });
        }

        var me = await _authService.GetCurrentUserAsync(userId);
        return Ok(me);
    }

    /// <summary>
    /// Elimina la foto de perfil (avatar) del usuario actualmente autenticado.
    /// </summary>
    [HttpDelete("perfil/avatar")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteMyAvatar()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var (success, error, notFound) = await _usuarioService.EliminarAvatarAsync(userId);
        if (notFound) return Unauthorized();

        var me = await _authService.GetCurrentUserAsync(userId);
        return Ok(me);
    }
}
