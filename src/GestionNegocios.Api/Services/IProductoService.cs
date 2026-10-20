using GestionNegocios.Api.DTOs;

namespace GestionNegocios.Api.Services;

public interface IProductoService
{
    Task<PagedResultDto<ProductoListDto>> GetPagedAsync(
        int page = 1,
        int pageSize = 10,
        string? search = null,
        int? categoriaId = null,
        bool? soloActivos = null);

    Task<List<ProductoListDto>> GetAllAsync(bool soloActivos = true);

    Task<ProductoDetailDto?> GetByIdAsync(int id);

    Task<(ProductoDetailDto? Created, string? Error)> CreateAsync(ProductoCreateDto dto);

    Task<(ProductoDetailDto? Updated, string? Error, bool NotFound)> UpdateAsync(int id, ProductoUpdateDto dto);

    Task<(bool Success, string? Error, bool Deactivated, bool NotFound)> DeleteAsync(int id);
}
