using System;
using System.Collections.Generic;

namespace Backend.Application.DTOs.StockTakes;

public class StockTakeItemDto
{
    public int Id { get; set; }
    public int StockTakeId { get; set; }
    public int? ProductVariantId { get; set; }
    public int? LocationId { get; set; }

    /// <summary>Lô hàng được gắn vào dòng kiểm kê (null nếu không theo lô).</summary>
    public int? PaddyLotId { get; set; }

    public string? SKU { get; set; }
    public string? ProductVariantName { get; set; }
    public decimal UnitWeightKg { get; set; }
    public string? LocationCode { get; set; }
    public string? ZoneName { get; set; }
    public string? LotCode { get; set; }
    public bool IsQuarantine { get; set; }

    public decimal SystemQuantity { get; set; }
    public decimal? ActualQuantity { get; set; }
    public decimal Difference { get; set; }

    /// <summary>Chênh lệch tuyệt đối theo kg (không dấu). Null khi chưa kiểm đếm.</summary>
    public decimal? AbsoluteVarianceKg { get; set; }

    /// <summary>Phần trăm chênh lệch so với tồn hệ thống. Null khi SystemQuantity = 0.</summary>
    public decimal? VariancePercent { get; set; }

    /// <summary>Mức độ chênh lệch: NONE / SMALL / MEDIUM / LARGE.</summary>
    public string VarianceSeverity { get; set; } = "NONE";

    public string? Note { get; set; }
    public bool QRScanned { get; set; }

    /// <summary>Đã xác nhận kiểm đếm lại (bắt buộc trước khi duyệt dòng LARGE).</summary>
    public bool RecountConfirmed { get; set; }

    public int? RecountConfirmedBy { get; set; }
    public DateTime? RecountConfirmedAt { get; set; }

    // ─── Kiểm kê theo BAO ────────────────────────────────────────────────────

    /// <summary>Số bao đang lưu tại (lô, vị trí) lúc chụp phiếu.</summary>
    public int SystemBagCount { get; set; }

    /// <summary>Số bao đếm được. Null = chưa kiểm đếm.</summary>
    public int? CountedBagCount { get; set; }

    /// <summary>Lệch số bao (âm = thiếu bao). Null khi chưa kiểm đếm.</summary>
    public int? BagDifference { get; set; }

    /// <summary>Số bao đã cân lại (phần còn lại giữ nguyên kg sổ sách).</summary>
    public int WeighedBagCount { get; set; }

    /// <summary>Tình trạng chất lượng: OK / WET / PEST / TORN_BAG / OTHER.</summary>
    public string QualityStatus { get; set; } = "OK";

    public string? QualityNote { get; set; }
    public string? QualityImageUrls { get; set; }

    /// <summary>Chất lượng không đạt (khác OK) — sẽ đề xuất cách ly lô khi duyệt.</summary>
    public bool IsQualityFailed { get; set; }

    /// <summary>Chi tiết từng bao trong phạm vi dòng này.</summary>
    public List<StockTakeItemBagDto> Bags { get; set; } = new();

    public List<StockTakeMovementEvidenceDto> PostSnapshotMovements { get; set; } = new();
}

/// <summary>Một bao trong dòng kiểm kê: số liệu sổ sách + kết quả kiểm đếm.</summary>
public class StockTakeItemBagDto
{
    public int Id { get; set; }
    public int StockTakeItemId { get; set; }
    public int PaddyLotBagId { get; set; }
    public int BagNo { get; set; }
    public string? QrCode { get; set; }

    public decimal SystemWeightKg { get; set; }

    /// <summary>Null = không cân bao này (được phép bỏ qua).</summary>
    public decimal? CountedWeightKg { get; set; }

    public bool Counted { get; set; }
    public bool ScannedByQr { get; set; }
    public bool IsUnexpected { get; set; }

    /// <summary>Có trong sổ nhưng không tìm thấy khi kiểm kê.</summary>
    public bool IsMissing { get; set; }

    /// <summary>Lệch kg của riêng bao này (0 khi không cân).</summary>
    public decimal WeightDifference { get; set; }

    public string QualityStatus { get; set; } = "OK";
    public string? Note { get; set; }
    public DateTime? CountedAt { get; set; }
    public int? CountedByUserId { get; set; }
}

public class StockTakeMovementEvidenceDto
{
    public int Id { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal BeforeQuantity { get; set; }
    public decimal AfterQuantity { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedDate { get; set; }
    public int? CreatedBy { get; set; }
}

public class CreateStockTakeItemDto
{
    public int? ProductVariantId { get; set; }
    public int? LocationId { get; set; }

    /// <summary>Lô hàng được chỉ định kiểm kê (null nếu sản phẩm không theo lô).</summary>
    public int? PaddyLotId { get; set; }

    public decimal SystemQuantity { get; set; }
    public decimal? ActualQuantity { get; set; }
    public string? Note { get; set; }
    public bool QRScanned { get; set; }

    /// <summary>Xác nhận đã kiểm đếm lại (gửi kèm khi mức chênh lệch là LARGE).</summary>
    public bool RecountConfirmed { get; set; }
}

public class UpdateStockTakeItemDto : CreateStockTakeItemDto
{
    public int Id { get; set; }
}
