using System;

namespace Backend.Application.DTOs.StockAlertConfigs;

/// <summary>
/// DTO tạo mới cấu hình ngưỡng cảnh báo tồn thấp theo kho và (tuỳ chọn) SKU.
/// </summary>
public class CreateStockAlertConfigDto
{
    public int WarehouseId { get; set; }

    /// <summary>null = áp dụng cho toàn kho (mọi SKU).</summary>
    public int? ProductVariantId { get; set; }

    /// <summary>Ngưỡng tồn tối thiểu (đơn vị theo SKU). Tồn khả dụng ≤ ngưỡng sẽ sinh cảnh báo.</summary>
    public decimal MinThreshold { get; set; }

    public bool IsActive { get; set; } = true;

    public int? CreatedBy { get; set; }
}
