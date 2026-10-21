using GestionNegocios.Api.DTOs;

namespace GestionNegocios.Api.Services;

public interface IVentaService
{
    Task<PagedResultDto<VentaListDto>> GetPagedAsync(
        int page = 1,
        int pageSize = 10,
        string? search = null,
        int? sucursalId = null,
        int? usuarioId = null,
        string? estado = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        int? userSucursalId = null,
        bool isAdmin = false);

    Task<VentaServiceResult> GetByIdAsync(int id, int? userSucursalId, bool isAdmin);

    Task<VentaServiceResult> CreateAsync(
        VentaCreateDto dto,
        int userId,
        int? userSucursalId,
        bool isAdmin);

    Task<VentaServiceResult> AnularAsync(int id, int? userSucursalId, bool isAdmin);
}
