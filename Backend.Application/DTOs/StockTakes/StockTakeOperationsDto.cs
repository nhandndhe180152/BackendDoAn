using System.Collections.Generic;

namespace Backend.Application.DTOs.StockTakes;

// ─────────────────────────────────────────────────────────────────────────────
// Lưu kết quả kiểm đếm — đơn vị là BAO. Backend tự tính lại số bao và tổng kg,
// không tin số tổng client gửi lên.
// ─────────────────────────────────────────────────────────────────────────────
public class SaveStockTakeCountsDto
{
    public string? Note { get; set; }
    public List<SaveStockTakeCountItemDto> Items { get; set; } = new();
}

public class SaveStockTakeCountItemDto
{
    public int Id { get; set; }
    public string? Note { get; set; }

    /// <summary>
    /// Chỉ dùng cho dòng tồn kho CŨ chưa quản lý theo bao (không có bao nào trong ảnh chụp).
    /// Dòng có bao thì backend tự tính tổng kg từ các bao và bỏ qua trường này.
    /// </summary>
    public decimal? ActualQuantity { get; set; }

    /// <summary>Lý do lệch — bắt buộc khi lệch số bao hoặc lệch kg.</summary>
    public string? VarianceReason { get; set; }

    /// <summary>Chỉnh lý số bao chốt lại (null = lấy đúng số bao đếm được).</summary>
    public int? AdjustedBagCount { get; set; }

    /// <summary>Chỉnh lý tổng kg chốt lại (null = lấy đúng tổng kg cân được).</summary>
    public decimal? AdjustedWeightKg { get; set; }

    public bool RecountConfirmed { get; set; }

    public List<SaveStockTakeCountBagDto> Bags { get; set; } = new();
}

public class SaveStockTakeCountBagDto
{
    /// <summary>Id của dòng StockTakeItemBag (ảnh chụp). 0 khi là bao phát sinh mới.</summary>
    public int Id { get; set; }

    /// <summary>Bắt buộc khi Id = 0 (bao phát sinh được quét thêm vào dòng).</summary>
    public int? PaddyLotBagId { get; set; }

    public bool Counted { get; set; }
    public bool ScannedByQr { get; set; }

    /// <summary>Kg cân lại. Null = không cân → giữ nguyên kg sổ sách.</summary>
    public decimal? CountedWeightKg { get; set; }

    // Chất lượng cấp bao
    public string? QualityResult { get; set; }
    public string? MoldLevel { get; set; }
    public string? PestLevel { get; set; }
    public string? PackagingStatus { get; set; }
    public decimal? MoisturePercent { get; set; }
    public decimal? ImpurityPercent { get; set; }
    public string? QualityNote { get; set; }

    // Xử lý bao
    public string? Disposition { get; set; }
    public int? TargetLocationId { get; set; }
    public string? DispositionNote { get; set; }
}

public class SubmitStockTakeDto
{
    public string? Note { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Quét QR một BAO trong lúc kiểm kê
// ─────────────────────────────────────────────────────────────────────────────
public class ScanStockTakeBagDto
{
    /// <summary>Chuỗi QR quét được (ưu tiên), ví dụ PLB-XXXX.</summary>
    public string? QrCode { get; set; }

    /// <summary>Dùng khi chọn bao từ danh sách thay vì quét.</summary>
    public int? PaddyLotBagId { get; set; }

    /// <summary>Kg cân được ngay lúc quét (tuỳ chọn).</summary>
    public decimal? CountedWeightKg { get; set; }
}

public class ScanStockTakeBagResultDto
{
    public bool Matched { get; set; }

    /// <summary>NOT_FOUND | OUT_OF_SCOPE | ALREADY_COUNTED | PULLED_IN | OK.</summary>
    public string Reason { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public int? StockTakeItemId { get; set; }
    public int? StockTakeItemBagId { get; set; }
    public int? PaddyLotBagId { get; set; }
    public int? BagNo { get; set; }
    public string? LotCode { get; set; }
    public string? ZoneName { get; set; }
    public string? LocationCode { get; set; }
    public decimal? SystemWeightKg { get; set; }
    public decimal? CountedWeightKg { get; set; }
    public int? PickSequence { get; set; }
    public int? RestowSequence { get; set; }
    public bool IsUnexpected { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Chọn CỘT cần kiểm kê (dropdown hoặc quét QR dán trên cột)
// ─────────────────────────────────────────────────────────────────────────────
public class StockTakeScopeOptionsDto
{
    public List<StockTakeColumnOptionDto> Columns { get; set; } = new();
}

public class StockTakeColumnOptionDto
{
    public int LocationId { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public string? LocationCode { get; set; }
    public string? QrCode { get; set; }
    public int BagCount { get; set; }
    public decimal TotalWeightKg { get; set; }
    public bool IsQuarantine { get; set; }
}

/// <summary>
/// Yêu cầu tra tem QR cột. Gửi bằng POST vì payload chứa ký tự '|' — nhét vào
/// query string là dễ bị encode/decode sai giữa app và server.
/// </summary>
public class ResolveScopeQrDto
{
    public string? QrCode { get; set; }

    /// <summary>Kho đang mở trên màn hình; chỉ dùng để soạn thông báo, không lọc.</summary>
    public int? WarehouseId { get; set; }
}

/// <summary>Kết quả quét QR dán trên cột để chọn phạm vi kiểm kê.</summary>
public class StockTakeScopeResolveDto
{
    public bool Matched { get; set; }

    /// <summary>Luôn là COLUMN khi khớp.</summary>
    public string? ScopeType { get; set; }
    public string Message { get; set; } = string.Empty;

    public string? ZoneName { get; set; }
    public int? LocationId { get; set; }
    public string? LocationCode { get; set; }
    public int? WarehouseId { get; set; }
    public bool IsQuarantine { get; set; }
    public int BagCount { get; set; }
    public decimal TotalWeightKg { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Gợi ý vị trí đích cho bao (ô cách ly khi QUARANTINE, cột thường khi RELEASE)
// ─────────────────────────────────────────────────────────────────────────────
public class StockTakeBagTargetSuggestionDto
{
    public int LocationId { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public string? LocationCode { get; set; }
    public bool IsQuarantine { get; set; }
    public decimal MaxCapacityKg { get; set; }
    public decimal CurrentOccupancyKg { get; set; }
    public decimal AvailableKg { get; set; }
    public decimal Score { get; set; }
    public string Reason { get; set; } = string.Empty;

    /// <summary>Vị trí backend gợi ý mặc định (điểm cao nhất).</summary>
    public bool IsRecommended { get; set; }
}
