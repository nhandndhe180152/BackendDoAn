using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Bao được chọn để chuyển kho nội bộ (theo BAO). Mỗi dòng chuyển kho
/// (<see cref="StockTransferItem"/>) có nhiều bao; mỗi bao lưu kết quả kiểm định
/// chất lượng tại bước lấy hàng ở kho nguồn và cách xử lý (đi tiếp / cách ly / bỏ).
/// Bao đạt chất lượng (QualityResult = PASS, Disposition = TRANSFER) mới thực sự
/// chuyển sang kho đích và được gắn <see cref="TargetLotId"/> (lô mới sinh ở đích)
/// khi nhận hàng. Bao không đạt được xử lý ngay tại kho nguồn.
/// </summary>
public class StockTransferBag : EntityAuditBase<int>
{
    public int StockTransferItemId { get; set; }
    public int BagId { get; set; }

    /// <summary>Lô nguồn đại diện của bao tại kho nguồn (để truy vết ngược).</summary>
    public int? SourceLotId { get; set; }

    /// <summary>Khối lượng bao (kg) tại thời điểm chọn.</summary>
    public decimal WeightKg { get; set; }

    // ── Kiểm định chất lượng từng bao tại kho nguồn ──────────────────────────
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

    public string? QualityNote { get; set; }

    /// <summary>TRANSFER | QUARANTINE | DISPOSE (xem StockTransferBagDispositions).</summary>
    public string Disposition { get; set; } = "TRANSFER";

    /// <summary>Ô cách ly ở kho nguồn khi Disposition = QUARANTINE (backend tự chọn nếu trống).</summary>
    public int? QuarantineLocationId { get; set; }

    /// <summary>Lô mới sinh ở kho đích khi nhận hàng (bao PASS).</summary>
    public int? TargetLotId { get; set; }

    public string? Note { get; set; }

    // Navigation
    public virtual StockTransferItem StockTransferItem { get; set; } = null!;
    public virtual PaddyLotBag Bag { get; set; } = null!;
    public virtual PaddyLot? SourceLot { get; set; }
    public virtual PaddyLot? TargetLot { get; set; }
    public virtual Location? QuarantineLocation { get; set; }
}
