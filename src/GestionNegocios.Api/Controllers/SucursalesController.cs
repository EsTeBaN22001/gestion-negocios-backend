using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionNegocios.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SucursalesController : ControllerBase
{
    private readonly ISucursalService _sucursalService;

    public SucursalesController(ISucursalService sucursalService)
    {
        _sucursalService = sucursalService;
    }

    /// <summary>
    /// Obtiene una lista paginada de sucursales con soporte de búsqueda.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<SucursalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] bool? soloActivas = null)
    {
        var result = await _sucursalService.GetPagedAsync(page, pageSize, search, soloActivas);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene la lista completa de sucursales (para selectores de formularios).
    /// </summary>
    [HttpGet("todas")]
    [ProducesResponseType(typeof(List<SucursalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool soloActivas = true)
    {
        var result = await _sucursalService.GetAllAsync(soloActivas);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene los detalles de una sucursal por su identificador.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SucursalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var sucursal = await _sucursalService.GetByIdAsync(id);
        if (sucursal == null) return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Sucursal no encontrada" });
        return Ok(sucursal);
    }

    /// <summary>
    /// Crea una nueva sucursal (Solo Administrador).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(SucursalDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] SucursalCreateDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var created = await _sucursalService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Actualiza los datos de una sucursal existente (Solo Administrador).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(SucursalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(int id, [FromBody] SucursalUpdateDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var updated = await _sucursalService.UpdateAsync(id, dto);
        if (updated == null) return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Sucursal no encontrada" });

        return Ok(updated);
    }

    /// <summary>
    /// Elimina físicamente o desactiva lógicamente una sucursal (Solo Administrador).
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, deactivated, message) = await _sucursalService.DeleteAsync(id);
        if (!success) return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Sucursal no encontrada", Detail = message });

        return Ok(new { message, deactivated });
    }
}
