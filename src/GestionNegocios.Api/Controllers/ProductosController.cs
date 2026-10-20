using GestionNegocios.Api.DTOs;
using GestionNegocios.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionNegocios.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductosController : ControllerBase
{
    private readonly IProductoService _productoService;

    public ProductosController(IProductoService productoService)
    {
        _productoService = productoService;
    }

    /// <summary>
    /// Obtiene una lista paginada de productos con filtros y búsqueda server-side.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<ProductoListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] int? categoriaId = null,
        [FromQuery] bool? soloActivos = null)
    {
        var result = await _productoService.GetPagedAsync(page, pageSize, search, categoriaId, soloActivos);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene la lista completa de productos para selectores rápidos.
    /// </summary>
    [HttpGet("todos")]
    [ProducesResponseType(typeof(List<ProductoListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool soloActivos = true)
    {
        var result = await _productoService.GetAllAsync(soloActivos);
        return Ok(result);
    }

    /// <summary>
    /// Obtiene el detalle de un producto por su identificador.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProductoDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var producto = await _productoService.GetByIdAsync(id);
        if (producto == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Producto no encontrado"
            });
        }
        return Ok(producto);
    }

    /// <summary>
    /// Crea un nuevo producto en el catálogo (Solo Administrador).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(ProductoDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] ProductoCreateDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var (created, error) = await _productoService.CreateAsync(dto);
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
    /// Modifica los datos de un producto existente (Solo Administrador).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(ProductoDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(int id, [FromBody] ProductoUpdateDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var (updated, error, notFound) = await _productoService.UpdateAsync(id, dto);
        if (notFound)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Producto no encontrado"
            });
        }

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

        return Ok(updated);
    }

    /// <summary>
    /// Elimina o desactiva de forma segura un producto (Solo Administrador).
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error, deactivated, notFound) = await _productoService.DeleteAsync(id);
        if (notFound)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Producto no encontrado"
            });
        }

        if (!success)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Operación no permitida",
                Detail = error
            });
        }

        if (deactivated)
        {
            return Ok(new
            {
                message = "El producto posee historial de ventas o existencias activas, por lo que se desactivó de forma lógica.",
                deactivated = true
            });
        }

        return Ok(new
        {
            message = "Producto eliminado definitivamente.",
            deactivated = false
        });
    }
}
