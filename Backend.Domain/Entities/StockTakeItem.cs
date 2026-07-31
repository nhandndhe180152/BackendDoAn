using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class StockTakeItem : EntityAuditBase<int>
{
    public int StockTakeId { get; set; }
    public int? ProductVariantId { get; set; }
    public int? LocationId { get; set; }

    /// <summary>
    /// Lô hàng cụ thể cần kiểm kê tại vị trí này.
    /// Null nếu sản phẩm không quản lý theo lô (hàng tổng hợp).
    /// </summary>
    public int? PaddyLotId { get; set; }

    public decimal SystemQuantity { get; set; }
    public decimal? ActualQuantity { get; set; }
    public string? Note { get; set; }
    public bool QRScanned { get; set; }

    // Computed — không lưu DB
    public decimal Difference => (ActualQuantity ?? 0) - SystemQuantity;

    /// <summary>
    /// Phần trăm chênh lệch so với tồn hệ thống.
    /// Trả về null khi: ActualQuantity chưa được nhập, hoặc SystemQuantity = 0 (không thể chia).
    /// </summary>
    public decimal? VariancePercent =>
        !ActualQuantity.HasValue || SystemQuantity == 0
            ? null
            : Math.Abs(Difference) / SystemQuantity * 100m;

    /// <summary>
    /// Phân loại mức độ chênh lệch. Giá trị được tính dựa trên VariancePercent.
    /// Ngưỡng SMALL / MEDIUM đọc từ SystemConfig (không hard-code ở đây).
    /// Thuộc tính này trả về <c>null</c>; việc ánh xạ sang label SMALL/MEDIUM/LARGE
    /// do StockTakeService thực hiện khi đọc ngưỡng từ DB.
    /// </summary>
    public string VarianceSeverity { get; set; } = "NONE";

    public virtual StockTake StockTake { get; set; } = null!;
    public virtual ProductVariant? ProductVariant { get; set; }
    public virtual Location? Location { get; set; }
    public virtual PaddyLot? PaddyLot { get; set; }
}
