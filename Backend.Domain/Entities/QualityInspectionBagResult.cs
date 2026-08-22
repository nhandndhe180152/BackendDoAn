using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Kết quả kiểm tra chất lượng cấp bao (bag-level).
/// Một inspection có nhiều bag-result; recheck khác inspection sẽ có InspectionId khác → giữ lịch sử.
/// Unique(QualityInspectionId, BagId): autosave/upsert một kết quả hiện hành của bag trong một inspection.
/// </summary>
public class QualityInspectionBagResult : EntityAuditBase<int>
{
    public int QualityInspectionId { get; set; }
    public int BagId { get; set; }

    /// <summary>
    /// Historical bag weight captured when the bag joins this inspection.
    /// Nullable only for rows created before the snapshot column existed.
    /// </summary>
    public decimal? BagWeightSnapshotKg { get; set; }

    public DateTime InspectedAt { get; set; }

    /// <summary>Inspector nullable — có thể do hệ thống ghi khi nhập hàng loạt.</summary>
    public int? InspectorId { get; set; }

    public decimal? MoisturePercent { get; set; }
    public decimal? ImpurityPercent { get; set; }

    /// <summary>Không / Nhẹ / Nặng</summary>
    public string? MoldLevel { get; set; }

    /// <summary>Không / Có dấu hiệu / Cần xử lý</summary>
    public string? PestLevel { get; set; }

    /// <summary>Nguyên / Rách / Ẩm</summary>
    public string? PackagingStatus { get; set; }

    /// <summary>PASS | ISSUE_DETECTED</summary>
    public string? QualityResult { get; set; }

    /// <summary>
    /// Receiving: ACCEPT_NORMAL | ACCEPT_QUARANTINE | REJECT_RETURN
    /// Storage:   KEEP_STORED  | QUARANTINE
    /// Recheck:   RELEASE      | KEEP_QUARANTINE
    /// Outbound:  RELEASE      | QUARANTINE
    /// </summary>
    public string? Disposition { get; set; }

    /// <summary>Phơi/Sấy/Đảo kho/Cách ly/Bán nhanh</summary>
    public string? Handling { get; set; }

    public string? Note { get; set; }

    // Navigation
    public virtual QualityInspection Inspection { get; set; } = null!;
    public virtual PaddyLotBag Bag { get; set; } = null!;
    public virtual User? Inspector { get; set; }
}
