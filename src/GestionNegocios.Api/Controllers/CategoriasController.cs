using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionNegocios.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriasController : ControllerBase
{
    private readonly ICategoriaService _categoriaService;

    public CategoriasController(ICategoriaService categoriaService)
    {
        _categoriaService = categoriaService;
    }

    /// <summary>
    /// Obtiene una lista paginada de categorías con soporte de búsqueda.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<CategoriaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] bool? soloActivas = null)
    {
        var result = await _categoriaService.GetPagedAsync(page, pageSize, search, soloActivas);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene la lista completa de categorías (para selectores de productos).
    /// </summary>
    [HttpGet("todas")]
    [ProducesResponseType(typeof(List<CategoriaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool soloActivas = true)
    {
        var result = await _categoriaService.GetAllAsync(soloActivas);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene los detalles de una categoría por su identificador.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(CategoriaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var categoria = await _categoriaService.GetByIdAsync(id);
        if (categoria == null) return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Categoría no encontrada" });
        return Ok(categoria);
    }

    /// <summary>
    /// Crea una nueva categoría (Solo Administrador).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(CategoriaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CategoriaCreateDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var (created, duplicateName) = await _categoriaService.CreateAsync(dto);
        if (duplicateName)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflicto",
                Detail = $"Ya existe una categoría con el nombre '{dto.Nombre}'."
            });
        }

        return CreatedAtAction(nameof(GetById), new { id = created!.Id }, created);
    }

    /// <summary>
    /// Actualiza los datos de una categoría existente (Solo Administrador).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(CategoriaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(int id, [FromBody] CategoriaUpdateDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var (updated, duplicateName, notFound) = await _categoriaService.UpdateAsync(id, dto);
        if (notFound) return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Categoría no encontrada" });
        if (duplicateName)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflicto",
                Detail = $"Ya existe otra categoría con el nombre '{dto.Nombre}'."
            });
        }

        return Ok(updated);
    }

    /// <summary>
    /// Elimina físicamente o desactiva lógicamente una categoría (Solo Administrador).
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, deactivated, message) = await _categoriaService.DeleteAsync(id);
        if (!success) return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Categoría no encontrada", Detail = message });

        return Ok(new { message, deactivated });
    }
}
