using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Ảnh chụp + kết quả kiểm kê của MỘT BAO trong một dòng kiểm kê.
/// Kiểm kê chạy theo đơn vị BAO: số lượng (đếm/cân) và chất lượng đều ghi ở cấp bao,
/// dòng cha <see cref="StockTakeItem"/> chỉ là tổng hợp lại.
/// Unique (StockTakeItemId, PaddyLotBagId) chặn quét trùng ngay ở tầng DB.
/// </summary>
public class StockTakeItemBag : EntityAuditBase<int>
{
    public int StockTakeItemId { get; set; }
    public int PaddyLotBagId { get; set; }

    // ── Ảnh chụp sổ sách tại thời điểm lập phiếu ──────────────────────────────
    public int BagNo { get; set; }
    public string? QrCode { get; set; }
    public decimal SystemWeightKg { get; set; }

    /// <summary>StackOrder trong cột tại lúc chụp (càng lớn càng nằm trên).</summary>
    public int SystemStackOrder { get; set; }

    /// <summary>
    /// Thứ tự LẤY RA: 1 = bao trên cùng của cột. Thủ kho lấy từ trên xuống dưới.
    /// </summary>
    public int PickSequence { get; set; }

    /// <summary>
    /// Thứ tự CẤT LẠI: bao lấy ra sau cùng được cất vào cột trước (LIFO đảo chiều).
    /// = (tổng số bao của cột) - PickSequence + 1.
    /// </summary>
    public int RestowSequence { get; set; }

    // ── Kết quả kiểm đếm ──────────────────────────────────────────────────────
    /// <summary>Đã tìm thấy bao ngoài thực tế. False = bao mất khỏi kho.</summary>
    public bool Counted { get; set; }

    /// <summary>Đánh dấu Counted bằng quét QR (bằng chứng mạnh hơn tích tay).</summary>
    public bool ScannedByQr { get; set; }

    /// <summary>Kg cân lại. Null = không cân, giữ nguyên kg sổ sách.</summary>
    public decimal? CountedWeightKg { get; set; }

    /// <summary>Bao phát sinh không có trong ảnh chụp (tìm thấy thừa tại vị trí).</summary>
    public bool IsUnexpected { get; set; }

    // ── Kết quả chất lượng cấp bao ────────────────────────────────────────────
    /// <summary>PASS | ISSUE_DETECTED. Null = chưa đánh giá chất lượng.</summary>
    public string? QualityResult { get; set; }

    /// <summary>Không / Nhẹ / Nặng</summary>
    public string? MoldLevel { get; set; }

    /// <summary>Không / Có dấu hiệu / Cần xử lý</summary>
    public string? PestLevel { get; set; }

    /// <summary>Nguyên / Rách / Ẩm</summary>
    public string? PackagingStatus { get; set; }

    public decimal? MoisturePercent { get; set; }
    public decimal? ImpurityPercent { get; set; }
    public string? QualityNote { get; set; }

    // ── Xử lý bao sau kiểm kê ─────────────────────────────────────────────────
    /// <summary>
    /// KEEP (giữ nguyên) | QUARANTINE (đưa sang khu cách ly) |
    /// DISPOSE (bao hỏng — bỏ cả bao ra khỏi kho) | RELEASE (rút khỏi cách ly về khu thường).
    /// </summary>
    public string Disposition { get; set; } = StockTakeBagDispositions.Keep;

    /// <summary>
    /// Vị trí đích khi QUARANTINE (ô cách ly) hoặc RELEASE (cột thường).
    /// Backend gợi ý sẵn, người dùng có thể chọn lại.
    /// </summary>
    public int? TargetLocationId { get; set; }

    public string? DispositionNote { get; set; }

    // ── Computed — EF Ignore ──────────────────────────────────────────────────
    /// <summary>
    /// Kg TÌM THẤY của bao khi kiểm kê: không tìm thấy → 0;
    /// có cân lại → kg cân; tìm thấy nhưng không cân → giữ nguyên kg sổ sách.
    /// Chưa trừ phần xử lý (cách ly / bỏ bao hỏng) — đó là bước sau.
    /// </summary>
    public decimal EffectiveWeightKg =>
        !Counted ? 0m : (CountedWeightKg ?? SystemWeightKg);

    /// <summary>Bao vẫn nằm lại đúng vị trí đang kiểm sau khi xử lý.</summary>
    public bool StaysAtLocation => Counted && Disposition == StockTakeBagDispositions.Keep;

    /// <summary>Bao rời khỏi vị trí đang kiểm (mất / bỏ hỏng / cách ly / rút cách ly).</summary>
    public bool LeavesLocation => !StaysAtLocation;

    public virtual StockTakeItem StockTakeItem { get; set; } = null!;
    public virtual PaddyLotBag PaddyLotBag { get; set; } = null!;
    public virtual Location? TargetLocation { get; set; }
}

/// <summary>Cách xử lý một bao sau khi kiểm kê.</summary>
public static class StockTakeBagDispositions
{
    /// <summary>Bao bình thường — giữ nguyên vị trí.</summary>
    public const string Keep = "KEEP";

    /// <summary>Bao có vấn đề chất lượng — chuyển sang ô cách ly.</summary>
    public const string Quarantine = "QUARANTINE";

    /// <summary>Bao hỏng — bỏ cả bao ra khỏi kho (ghi giảm tồn toàn bộ bao).</summary>
    public const string Dispose = "DISPOSE";

    /// <summary>Kiểm kê ô cách ly đạt — rút bao ra, cất về cột thường.</summary>
    public const string Release = "RELEASE";

    public static readonly string[] All = [Keep, Quarantine, Dispose, Release];

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Array.IndexOf(All, value) >= 0;
}
