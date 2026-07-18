namespace Backend.Domain.DTParameters;

/// <summary>
/// Bộ lọc cho tổng hợp KPI tồn kho (POST /inventories/summary).
/// Khớp với bộ lọc của bảng để 5 thẻ và bảng luôn đồng bộ.
/// </summary>
public class InventorySummaryParameters
{
    public int? WarehouseId { get; set; }

    public int? LocationId { get; set; }

    public int? ProductCategoryId { get; set; }

    public string? LotType { get; set; }
}
