using System.Security.Claims;
using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionNegocios.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VentasController : ControllerBase
{
    private readonly IVentaService _ventaService;

    public VentasController(IVentaService ventaService)
    {
        _ventaService = ventaService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<VentaListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] int? sucursalId = null,
        [FromQuery] int? usuarioId = null,
        [FromQuery] string? estado = null,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null)
    {
        var result = await _ventaService.GetPagedAsync(
            page,
            pageSize,
            search,
            sucursalId,
            usuarioId,
            estado,
            fechaDesde,
            fechaHasta,
            GetCurrentUserSucursalId(),
            IsAdmin());

        return Ok(result);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(VentaDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _ventaService.GetByIdAsync(id, GetCurrentUserSucursalId(), IsAdmin());
        return ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(VentaDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] VentaCreateDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _ventaService.CreateAsync(dto, userId.Value, GetCurrentUserSucursalId(), IsAdmin());
        if (result.Status == VentaResultStatus.Success)
        {
            return CreatedAtAction(nameof(GetById), new { id = result.Venta!.Id }, result.Venta);
        }

        return ToActionResult(result);
    }

    [HttpPost("{id}/anular")]
    [ProducesResponseType(typeof(VentaDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Anular(int id)
    {
        var result = await _ventaService.AnularAsync(id, GetCurrentUserSucursalId(), IsAdmin());
        return ToActionResult(result);
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim, out var id) ? id : null;
    }

    private int? GetCurrentUserSucursalId()
    {
        var sucursalClaim = User.FindFirstValue("sucursalId");
        return int.TryParse(sucursalClaim, out var id) ? id : null;
    }

    private bool IsAdmin() => User.IsInRole("Administrador");

    private IActionResult ToActionResult(VentaServiceResult result)
    {
        return result.Status switch
        {
            VentaResultStatus.Success => Ok(result.Venta),
            VentaResultStatus.Forbidden => Forbid(),
            VentaResultStatus.NotFound => NotFound(CreateProblemDetails(StatusCodes.Status404NotFound, "Recurso no encontrado", result.Error)),
            VentaResultStatus.Conflict => Conflict(CreateProblemDetails(StatusCodes.Status409Conflict, "Conflicto", result.Error)),
            _ => BadRequest(CreateProblemDetails(StatusCodes.Status400BadRequest, "Error de validación", result.Error))
        };
    }

    private static ProblemDetails CreateProblemDetails(int status, string title, string? detail)
    {
        return new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
    }
}
