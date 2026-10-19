using GestionNegocios.Api.DTOs;

namespace GestionNegocios.Api.Services;

public interface ICategoriaService
{
    Task<PagedResultDto<CategoriaDto>> GetPagedAsync(int page = 1, int pageSize = 10, string? search = null, bool? soloActivas = null);
    Task<List<CategoriaDto>> GetAllAsync(bool soloActivas = true);
    Task<CategoriaDto?> GetByIdAsync(int id);
    Task<(CategoriaDto? result, bool duplicateName)> CreateAsync(CategoriaCreateDto dto);
    Task<(CategoriaDto? result, bool duplicateName, bool notFound)> UpdateAsync(int id, CategoriaUpdateDto dto);
    Task<(bool success, bool deactivated, string? message)> DeleteAsync(int id);
}
