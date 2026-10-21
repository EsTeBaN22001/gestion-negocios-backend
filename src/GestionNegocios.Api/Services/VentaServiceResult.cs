using GestionNegocios.Api.DTOs;

namespace GestionNegocios.Api.Services;

public enum VentaResultStatus
{
    Success,
    ValidationError,
    NotFound,
    Forbidden,
    Conflict
}

public sealed record VentaServiceResult(
    VentaResultStatus Status,
    VentaDetailDto? Venta = null,
    string? Error = null);
