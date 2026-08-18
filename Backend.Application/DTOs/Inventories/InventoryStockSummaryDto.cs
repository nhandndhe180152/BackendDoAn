namespace Backend.Application.DTOs.Inventories;

/// <summary>
/// Tổng hợp tồn kho theo trạng thái cho 5 thẻ KPI màn "Giám sát tồn kho":
/// Tồn thực tế / Khả dụng / Đã giữ / Đang xử lý / Cách ly.
/// Cung cấp cả số lượng (đơn vị) và khối lượng quy đổi (kg) để hiển thị theo tấn.
/// </summary>
public class InventoryStockSummaryDto
{
    // ----- Theo số lượng (đơn vị tồn) -----
    public decimal TotalOnHand { get; set; }
    public decimal TotalAvailable { get; set; }
    public decimal TotalReserved { get; set; }
    public decimal TotalProcessing { get; set; }
    public decimal TotalQuarantine { get; set; }

    // ----- Theo khối lượng quy đổi (kg) -----
    public decimal TotalOnHandWeightKg { get; set; }
    public decimal TotalAvailableWeightKg { get; set; }
    public decimal TotalReservedWeightKg { get; set; }
    public decimal TotalProcessingWeightKg { get; set; }
    public decimal TotalQuarantineWeightKg { get; set; }

    /// <summary>Số dòng tồn (inventory rows) sau khi lọc.</summary>
    public int LineCount { get; set; }

    /// <summary>Số lô đang cách ly.</summary>
    public int QuarantineLotCount { get; set; }

    /// <summary>Số dòng dưới mức tồn tối thiểu (low-stock).</summary>
    public int LowStockCount { get; set; }
}
