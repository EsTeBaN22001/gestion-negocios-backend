using System.Security.Claims;
using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionNegocios.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StockController : ControllerBase
{
    private readonly IStockService _stockService;

    public StockController(IStockService stockService)
    {
        _stockService = stockService;
    }

    private int? GetCurrentUserSucursalId()
    {
        var sucursalClaim = User.FindFirstValue("sucursalId");
        return int.TryParse(sucursalClaim, out var id) ? id : null;
    }

    private bool IsAdmin()
    {
        return User.IsInRole("Administrador");
    }

    /// <summary>
    /// Obtiene una lista paginada del inventario físico.
    /// Para Empleados, se restringe forzosamente a su sucursal asignada.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<StockListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? sucursalId = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? soloBajoStock = null)
    {
        var userSucursalId = GetCurrentUserSucursalId();
        var isAdmin = IsAdmin();

        var result = await _stockService.GetPagedAsync(
            page,
            pageSize,
            sucursalId,
            search,
            soloBajoStock,
            userSucursalId,
            isAdmin);

        return Ok(result);
    }

    /// <summary>
    /// Obtiene las existencias de stock de una sucursal específica.
    /// </summary>
    [HttpGet("sucursal/{sucursalId}")]
    [ProducesResponseType(typeof(List<StockListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetBySucursal(int sucursalId)
    {
        var userSucursalId = GetCurrentUserSucursalId();
        var isAdmin = IsAdmin();

        if (!isAdmin && userSucursalId != sucursalId)
        {
            return Forbid();
        }

        var result = await _stockService.GetBySucursalAsync(sucursalId, userSucursalId, isAdmin);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene el detalle de un registro de stock.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(StockDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var userSucursalId = GetCurrentUserSucursalId();
        var isAdmin = IsAdmin();

        var stock = await _stockService.GetByIdAsync(id, userSucursalId, isAdmin);
        if (stock == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Registro de stock no encontrado"
            });
        }

        return Ok(stock);
    }

    /// <summary>
    /// Asigna o inicializa el stock de un producto en una sucursal (Solo Administrador).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(StockDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] StockCreateDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var (created, error) = await _stockService.CreateAsync(dto);
        if (error != null)
        {
            if (error.Contains("ya existe", StringComparison.OrdinalIgnoreCase))
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
    /// Ajusta manualmente las existencias o el stock mínimo de un producto (Solo Administrador).
    /// </summary>
    [HttpPut("{id}/ajuste")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(StockDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Ajustar(int id, [FromBody] StockAjusteDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var (updated, error, notFound) = await _stockService.AjustarAsync(id, dto);
        if (notFound)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Registro de stock no encontrado"
            });
        }

        if (error != null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Error en ajuste de inventario",
                Detail = error
            });
        }

        return Ok(updated);
    }
}
