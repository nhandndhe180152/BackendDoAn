using System;
using System.Collections.Generic;

namespace Backend.Application.DTOs.StockTransfers;

public class StockTransferDetailDto
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public string TransferCode { get; set; } = null!;
    public int StatusId { get; set; }
    public string? StatusName { get; set; }
    public string? StatusCode { get; set; }
    public string? StatusColor { get; set; }
    public int FromWarehouseId { get; set; }
    public string? FromWarehouseName { get; set; }
    public int ToWarehouseId { get; set; }
    public string? ToWarehouseName { get; set; }
    public int? AssignedUserId { get; set; }
    public DateTime TransferDate { get; set; }
    public string? Note { get; set; }
    public int ItemCount { get; set; }
    public decimal TotalWeightKg { get; set; }
    public List<StockTransferItemDetailDto> Items { get; set; } = new();

    /// <summary>Phiếu nhập kho tự sinh ở kho đích (truy vết nguồn = kho nguồn). Có khi đã Hoàn tất.</summary>
    public int? DestinationInboundOrderId { get; set; }
    public string? DestinationInboundOrderCode { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}

public class StockTransferItemDetailDto
{
    public int Id { get; set; }
    public int ProductVariantId { get; set; }
    public string? SKU { get; set; }
    public string? ProductVariantName { get; set; }
    public int? PaddyLotId { get; set; }
    public string? LotCode { get; set; }
    public int? FromLocationId { get; set; }
    public string? FromLocationName { get; set; }
    public int? ToLocationId { get; set; }
    public string? ToLocationName { get; set; }
    public decimal WeightKg { get; set; }
    public string? Note { get; set; }
    public List<int> BagIds { get; set; } = new();

    /// <summary>Chi tiết từng bao (theo BAO) kèm chất lượng + cách xử lý + truy vết lô.</summary>
    public List<StockTransferBagDetailDto> Bags { get; set; } = new();
}

public class StockTransferBagDetailDto
{
    public int Id { get; set; }
    public int BagId { get; set; }
    public int? BagNo { get; set; }
    public string? QrCode { get; set; }
    public int? SourceLotId { get; set; }
    public string? SourceLotCode { get; set; }
    public decimal WeightKg { get; set; }
    public decimal? MoisturePercent { get; set; }
    public decimal? ImpurityPercent { get; set; }
    public string? MoldLevel { get; set; }
    public string? PestLevel { get; set; }
    public string? PackagingStatus { get; set; }
    public string? QualityResult { get; set; }
    public string? QualityNote { get; set; }
    public string Disposition { get; set; } = "TRANSFER";
    public int? QuarantineLocationId { get; set; }
    public string? QuarantineLocationName { get; set; }
    public int? TargetLotId { get; set; }
    public string? TargetLotCode { get; set; }
    public string? Note { get; set; }
}

/// <summary>Bao ở đỉnh cột nguồn có thể chọn để chuyển kho (picker).</summary>
public class SourceColumnBagDto
{
    public int BagId { get; set; }
    public int BagNo { get; set; }
    public string? QrCode { get; set; }
    public int StackOrder { get; set; }
    public decimal WeightKg { get; set; }
    public bool IsFull { get; set; }
    public int? LotId { get; set; }
    public string? LotCode { get; set; }
    public int ProductVariantId { get; set; }
    public string? ProductVariantName { get; set; }
    /// <summary>Chất lượng lô hiện tại (PASSED/FAILED...) để tham chiếu khi nhập kiểm định.</summary>
    public string? LotQualityStatus { get; set; }
}

/// <summary>Gợi ý ô lưu ở kho đích (chỉ tên vị trí — không kèm điểm/lý do chấm).</summary>
public class LocationSuggestionDto
{
    public int LocationId { get; set; }
    public string? LocationName { get; set; }
}

public class StockTransferSummaryDto
{
    public int TransfersThisMonth { get; set; }
    public int InTransitCount { get; set; }
    public decimal TotalTransferredWeightKg { get; set; }
}

public class CancelStockTransferDto
{
    public string? Reason { get; set; }
}
