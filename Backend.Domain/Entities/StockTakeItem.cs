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

    /// <summary>
    /// Xác nhận đã kiểm đếm lại (bắt buộc cho dòng có mức chênh lệch LARGE).
    /// Lưu DB để giữ bằng chứng audit.
    /// </summary>
    public bool RecountConfirmed { get; set; }

    /// <summary>
    /// ID người xác nhận kiểm đếm lại. Null khi chưa xác nhận hoặc không cần.
    /// </summary>
    public int? RecountConfirmedBy { get; set; }

    /// <summary>
    /// Thời điểm người dùng xác nhận kiểm đếm lại (theo giờ Việt Nam).
    /// </summary>
    public DateTime? RecountConfirmedAt { get; set; }

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
    /// Chênh lệch tuyệt đối tính theo kg (không có dấu âm/dương).
    /// Dùng cho phân loại theo ngưỡng kg trong FDS.
    /// </summary>
    public decimal AbsoluteVarianceKg =>
        ActualQuantity.HasValue ? Math.Abs(Difference) : 0m;

    /// <summary>
    /// Mức độ chênh lệch khi kiểm kê: NONE / SMALL / MEDIUM / LARGE.
    /// Mặc định là "NONE"; được StockTakeService ghi đè sau khi đọc ngưỡng từ SystemConfig.
    /// Không được lưu vào DB (EF Ignore).
    /// </summary>
    public string VarianceSeverity { get; set; } = "NONE";

    public virtual StockTake StockTake { get; set; } = null!;
    public virtual ProductVariant? ProductVariant { get; set; }
    public virtual Location? Location { get; set; }
    public virtual PaddyLot? PaddyLot { get; set; }
    public virtual User? RecountConfirmedByUser { get; set; }
}
