using System;
using System.Collections.Generic;

namespace Backend.Application.DTOs.StockTransfers;

public class StockTransferItemDto
{
    public int ProductVariantId { get; set; }
    public int? PaddyLotId { get; set; }
    public int? FromLocationId { get; set; }
    public int? ToLocationId { get; set; }
    public decimal WeightKg { get; set; }
    public string? Note { get; set; }

    /// <summary>Danh sách BagId (luồng cũ — tương thích ngược). Ưu tiên dùng <see cref="Bags"/>.</summary>
    public List<int> BagIds { get; set; } = new();

    /// <summary>Danh sách bao theo BAO kèm kiểm định chất lượng + cách xử lý (luồng mới).</summary>
    public List<StockTransferBagInputDto> Bags { get; set; } = new();
}

/// <summary>
/// Một bao được chọn từ cột nguồn: kết quả kiểm định chất lượng và cách xử lý.
/// - QualityResult = PASS  → Disposition phải là TRANSFER (chuyển đi).
/// - QualityResult = ISSUE_DETECTED → Disposition phải là QUARANTINE hoặc DISPOSE.
/// </summary>
public class StockTransferBagInputDto
{
    public int BagId { get; set; }

    public decimal? MoisturePercent { get; set; }
    public decimal? ImpurityPercent { get; set; }
    public string? MoldLevel { get; set; }
    public string? PestLevel { get; set; }
    public string? PackagingStatus { get; set; }

    /// <summary>PASS | ISSUE_DETECTED</summary>
    public string? QualityResult { get; set; }

    /// <summary>TRANSFER | QUARANTINE | DISPOSE</summary>
    public string? Disposition { get; set; }

    /// <summary>Ô cách ly ở kho nguồn khi Disposition = QUARANTINE. Để trống → backend tự chọn.</summary>
    public int? QuarantineLocationId { get; set; }

    public string? QualityNote { get; set; }
    public string? Note { get; set; }
}

public class CreateStockTransferDto
{
    public int? OrganizationId { get; set; }
    public int FromWarehouseId { get; set; }
    public int ToWarehouseId { get; set; }
    public DateTime TransferDate { get; set; }
    public int? AssignedUserId { get; set; }
    public string? Note { get; set; }
    public List<StockTransferItemDto> Items { get; set; } = new();
    public int? CreatedBy { get; set; }
}
