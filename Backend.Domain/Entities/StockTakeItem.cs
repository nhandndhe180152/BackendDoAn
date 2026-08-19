using System;
using System.Collections.Generic;
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

    /// <summary>Tổng kg sổ sách của dòng (= tổng kg các bao được chụp).</summary>
    public decimal SystemQuantity { get; set; }

    /// <summary>Tổng kg thực tế — backend TỰ TÍNH lại từ các bao, không tin client.</summary>
    public decimal? ActualQuantity { get; set; }

    /// <summary>Số bao sổ sách tại vị trí này khi lập phiếu.</summary>
    public int SystemBagCount { get; set; }

    /// <summary>Số bao đếm được thực tế. Null = dòng chưa kiểm.</summary>
    public int? CountedBagCount { get; set; }

    /// <summary>
    /// Lý do lệch — BẮT BUỘC khi lệch số bao hoặc lệch kg.
    /// Tách khỏi Note để báo cáo chênh lệch có trường riêng.
    /// </summary>
    public string? VarianceReason { get; set; }

    /// <summary>
    /// Chỉnh lý sau kiểm kê: số bao chốt lại. Null = lấy đúng số bao đếm được.
    /// </summary>
    public int? AdjustedBagCount { get; set; }

    /// <summary>
    /// Chỉnh lý sau kiểm kê: tổng kg chốt lại. Null = lấy đúng tổng kg cân được.
    /// </summary>
    public decimal? AdjustedWeightKg { get; set; }

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

    /// <summary>Chênh lệch số bao (âm = thiếu bao).</summary>
    public int BagDifference => (CountedBagCount ?? 0) - SystemBagCount;

    /// <summary>Có lệch số bao hay không (dòng chưa kiểm coi như chưa lệch).</summary>
    public bool HasBagVariance => CountedBagCount.HasValue && BagDifference != 0;

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
    public virtual ICollection<StockTakeItemBag> Bags { get; set; } = new List<StockTakeItemBag>();
}
