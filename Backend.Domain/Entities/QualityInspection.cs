using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Ghi nhận kiểm tra chất lượng định kỳ theo lô.
/// Căn cứ cảnh báo rủi ro và quyết định cách ly.
/// </summary>
public class QualityInspection : EntityAuditBase<int>
{
    public int PaddyLotId { get; set; }
    public DateTime InspectedAt { get; set; }
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

    /// <summary>Kết quả tổng: Đạt / Không đạt</summary>
    public bool PassedInspection { get; set; }

    public string? Note { get; set; }
    public decimal? AffectedWeightKg { get; set; }

    // Navigation
    public virtual PaddyLot PaddyLot { get; set; } = null!;
    public virtual User? Inspector { get; set; }
}
