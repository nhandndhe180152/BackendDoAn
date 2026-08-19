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
    public bool IsOutboundStaging { get; set; }

    public decimal SystemQuantity { get; set; }
    public decimal? ActualQuantity { get; set; }
    public decimal Difference { get; set; }

    /// <summary>Số bao sổ sách tại vị trí này.</summary>
    public int SystemBagCount { get; set; }

    /// <summary>Số bao đếm được. Null = dòng chưa kiểm.</summary>
    public int? CountedBagCount { get; set; }

    /// <summary>Chênh lệch số bao (âm = thiếu bao).</summary>
    public int BagDifference { get; set; }

    /// <summary>Lý do lệch (bắt buộc khi lệch bao hoặc lệch kg).</summary>
    public string? VarianceReason { get; set; }

    /// <summary>Chỉnh lý: số bao chốt lại.</summary>
    public int? AdjustedBagCount { get; set; }

    /// <summary>Chỉnh lý: tổng kg chốt lại.</summary>
    public decimal? AdjustedWeightKg { get; set; }

    /// <summary>Số bao chuyển sang khu cách ly.</summary>
    public int QuarantineBagCount { get; set; }

    /// <summary>Số bao hỏng bị bỏ ra.</summary>
    public int DisposedBagCount { get; set; }

    /// <summary>Số bao rút khỏi cách ly để cất về khu thường.</summary>
    public int ReleasedBagCount { get; set; }

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

    public List<StockTakeMovementEvidenceDto> PostSnapshotMovements { get; set; } = new();

    /// <summary>Chi tiết từng bao — nguồn sự thật của dòng kiểm kê.</summary>
    public List<StockTakeItemBagDto> Bags { get; set; } = new();
}

/// <summary>Một bao trong dòng kiểm kê: ảnh chụp + kết quả đếm/cân/chất lượng/xử lý.</summary>
public class StockTakeItemBagDto
{
    public int Id { get; set; }
    public int StockTakeItemId { get; set; }
    public int PaddyLotBagId { get; set; }
    public int BagNo { get; set; }
    public string? QrCode { get; set; }
    public string? LotCode { get; set; }
    public decimal SystemWeightKg { get; set; }
    public int SystemStackOrder { get; set; }

    /// <summary>Thứ tự LẤY RA (1 = bao trên cùng cột).</summary>
    public int PickSequence { get; set; }

    /// <summary>Thứ tự CẤT LẠI (bao lấy ra sau cùng cất vào cột trước).</summary>
    public int RestowSequence { get; set; }

    public bool Counted { get; set; }
    public bool ScannedByQr { get; set; }
    public decimal? CountedWeightKg { get; set; }
    public decimal EffectiveWeightKg { get; set; }
    public bool IsUnexpected { get; set; }

    public string? QualityResult { get; set; }
    public string? MoldLevel { get; set; }
    public string? PestLevel { get; set; }
    public string? PackagingStatus { get; set; }
    public decimal? MoisturePercent { get; set; }
    public decimal? ImpurityPercent { get; set; }
    public string? QualityNote { get; set; }

    public string Disposition { get; set; } = "KEEP";
    public int? TargetLocationId { get; set; }
    public string? TargetLocationCode { get; set; }
    public string? TargetZoneName { get; set; }
    public string? DispositionNote { get; set; }
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
