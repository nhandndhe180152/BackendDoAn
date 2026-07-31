using System;

namespace Backend.Application.DTOs.StockTakes;

public class StockTakeItemDto
{
    public int Id { get; set; }
    public int StockTakeId { get; set; }
    public int? ProductVariantId { get; set; }
    public int? LocationId { get; set; }

    /// <summary>Lô hàng được gắn vào dòng kiểm kê (null nếu không theo lô).</summary>
    public int? PaddyLotId { get; set; }

    public decimal SystemQuantity { get; set; }
    public decimal? ActualQuantity { get; set; }
    public decimal Difference { get; set; }

    /// <summary>Phần trăm chênh lệch so với tồn hệ thống. Null khi SystemQuantity = 0.</summary>
    public decimal? VariancePercent { get; set; }

    /// <summary>Mức độ chênh lệch: NONE / SMALL / MEDIUM / LARGE.</summary>
    public string VarianceSeverity { get; set; } = "NONE";

    public string? Note { get; set; }
    public bool QRScanned { get; set; }
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
}

public class UpdateStockTakeItemDto : CreateStockTakeItemDto
{
    public int Id { get; set; }
}

