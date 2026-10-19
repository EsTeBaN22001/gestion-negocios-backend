using GestionNegocios.Api.DTOs;

namespace GestionNegocios.Api.Services;

public interface ISucursalService
{
    Task<PagedResultDto<SucursalDto>> GetPagedAsync(int page = 1, int pageSize = 10, string? search = null, bool? soloActivas = null);
    Task<List<SucursalDto>> GetAllAsync(bool soloActivas = true);
    Task<SucursalDto?> GetByIdAsync(int id);
    Task<SucursalDto> CreateAsync(SucursalCreateDto dto);
    Task<SucursalDto?> UpdateAsync(int id, SucursalUpdateDto dto);
    Task<(bool success, bool deactivated, string? message)> DeleteAsync(int id);
}
