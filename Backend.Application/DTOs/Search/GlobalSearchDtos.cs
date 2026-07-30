using System.Collections.Generic;

namespace Backend.Application.DTOs.Search;

/// <summary>
/// Một nhóm kết quả tìm kiếm toàn cục (theo loại đối tượng), phục vụ thanh tìm kiếm trên header.
/// </summary>
public class GlobalSearchGroupDto
{
    /// <summary>Mã loại: PRODUCT | PRODUCT_VARIANT | PADDY_LOT | SALES_ORDER | INBOUND_ORDER | CUSTOMER | FARMER | SUPPLIER.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Nhãn hiển thị tiếng Việt của nhóm.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Đường dẫn màn danh sách tương ứng (FE điều hướng, kèm ?q=keyword).</summary>
    public string Url { get; set; } = string.Empty;

    public List<GlobalSearchItemDto> Items { get; set; } = new();
}

public class GlobalSearchItemDto
{
    public int Id { get; set; }

    /// <summary>Dòng chính hiển thị (tên/mã).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Dòng phụ (SKU, SĐT, khách hàng...).</summary>
    public string? Subtitle { get; set; }
}
