using GestionNegocios.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace GestionNegocios.Api.Controllers;

/// <summary>
/// Endpoint técnico de verificación de infraestructura.
/// No representa ninguna funcionalidad de negocio.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HealthController> _logger;

    public HealthController(ApplicationDbContext context, ILogger<HealthController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Verifica que la API está activa.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            timestamp = DateTime.UtcNow,
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
        });
    }

    /// <summary>
    /// Verifica que la base de datos es accesible.
    /// </summary>
    [HttpGet("db")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetDb()
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync();
            if (canConnect)
            {
                return Ok(new
                {
                    status = "ok",
                    database = "connected",
                    timestamp = DateTime.UtcNow
                });
            }

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "error",
                database = "unreachable",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar la conexión a la base de datos.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "error",
                database = "error",
                message = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }
}
