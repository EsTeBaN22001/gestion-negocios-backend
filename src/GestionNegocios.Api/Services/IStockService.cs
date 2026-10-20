using GestionNegocios.Api.DTOs;

namespace GestionNegocios.Api.Services;

public interface IStockService
{
    Task<PagedResultDto<StockListDto>> GetPagedAsync(
        int page = 1,
        int pageSize = 10,
        int? sucursalId = null,
        string? search = null,
        bool? soloBajoStock = null,
        int? userSucursalId = null,
        bool isAdmin = false);

    Task<List<StockListDto>> GetBySucursalAsync(int sucursalId, int? userSucursalId, bool isAdmin);

    Task<StockDetailDto?> GetByIdAsync(int id, int? userSucursalId, bool isAdmin);

    Task<(StockDetailDto? Created, string? Error)> CreateAsync(StockCreateDto dto);

    Task<(StockDetailDto? Updated, string? Error, bool NotFound)> AjustarAsync(int id, StockAjusteDto dto);
}
