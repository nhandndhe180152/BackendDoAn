using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Ghi nhận kiểm tra chất lượng định kỳ theo lô.
/// Căn cứ cảnh báo rủi ro và quyết định cách ly.
/// Nguồn sự thật mới cho kết quả bag-level: xem QualityInspectionBagResult.
/// </summary>
public class QualityInspection : EntityAuditBase<int>
{
    public int PaddyLotId { get; set; }
    public DateTime InspectedAt { get; set; }

    /// <summary>
    /// Loại kiểm tra: RECEIVING | STORAGE | RECHECK | OUTBOUND_EXCEPTION.
    /// Nullable để dữ liệu cũ (LEGACY) không bị mất; service mới bắt buộc set.
    /// </summary>
    public string? InspectionType { get; set; }

    // ── Fields tổng hợp aggregate (compat với report/FE cũ) ──────────────────
    // Không xoá ở phase này. Sau complete, service cập nhật từ BagResults.
    public decimal? MoisturePercent { get; set; }
    public decimal? ImpurityPercent { get; set; }

    /// <summary>Không / Nhẹ / Nặng</summary>
    public string? MoldLevel { get; set; }

    /// <summary>Không / Có dấu hiệu / Cần xử lý</summary>
    public string? PestLevel { get; set; }

    /// <summary>Nguyên / Rách / Ẩm</summary>
    public string? PackagingStatus { get; set; }

    /// <summary>Phơi/Sấy/Đảo kho/Cách ly/Bán nhanh</summary>
    public string? Handling { get; set; }

    public int? InspectorId { get; set; }

    /// <summary>Kết quả tổng hợp: tất cả bag PASS = true. Aggregate từ BagResults khi complete.</summary>
    public bool PassedInspection { get; set; }

    public string? Note { get; set; }

    /// <summary>Tổng weight bag không ACCEPT_NORMAL/KEEP_STORED. Aggregate từ BagResults khi complete.</summary>
    public decimal? AffectedWeightKg { get; set; }

    // ── Header completion ────────────────────────────────────────────────────
    public DateTime? CompletedAt { get; set; }
    public int? CompletedBy { get; set; }

    // Navigation
    public virtual PaddyLot PaddyLot { get; set; } = null!;
    public virtual User? Inspector { get; set; }
    public virtual ICollection<QualityInspectionBagResult> BagResults { get; set; }
        = new List<QualityInspectionBagResult>();
}
