namespace GestionNegocios.Api.DTOs;

/// <summary>
/// DTO genérico para respuestas paginadas según docs/11-pagination-search.md.
/// </summary>
public class PagedResultDto<T>
{
    public IEnumerable<T> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }

    public PagedResultDto() { }

    public PagedResultDto(IEnumerable<T> items, int page, int pageSize, int totalItems)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalItems = totalItems;
        TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalItems / pageSize) : 0;
    }
}
