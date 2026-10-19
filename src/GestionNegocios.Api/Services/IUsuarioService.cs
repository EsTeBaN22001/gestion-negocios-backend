using GestionNegocios.Api.DTOs;

namespace GestionNegocios.Api.Services;

public interface IUsuarioService
{
    Task<PagedResultDto<UsuarioListDto>> GetPagedAsync(int page = 1, int pageSize = 10, string? search = null, string? rol = null, int? sucursalId = null, bool? soloActivos = null);
    Task<UsuarioDetailDto?> GetByIdAsync(int id);
    Task<(UsuarioDetailDto? result, string? error)> CreateAsync(UsuarioCreateDto dto);
    Task<(UsuarioDetailDto? result, string? error, bool notFound)> UpdateAsync(int id, UsuarioUpdateDto dto, int currentUserId);
    Task<(bool success, string? error)> ToggleActivoAsync(int id, int currentUserId);
}
