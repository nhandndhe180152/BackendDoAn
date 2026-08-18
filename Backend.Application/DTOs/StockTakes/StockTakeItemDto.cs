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
