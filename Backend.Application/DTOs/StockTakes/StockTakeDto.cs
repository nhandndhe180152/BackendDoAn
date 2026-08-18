using System;
using System.Collections.Generic;

namespace Backend.Application.DTOs.StockTakes;

public class StockTakeDto
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public int StockTakeStatusId { get; set; }
    public string STCode { get; set; } = null!;
    public string? Note { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int? ApprovedByUserId { get; set; }
    public string? ApproveNote { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }

    public string? WarehouseName { get; set; }
    public string? StockTakeStatusCode { get; set; }
    public string? StockTakeStatusName { get; set; }
    public string? StockTakeStatusColor { get; set; }
    public int? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public string ScopeDisplay { get; set; } = string.Empty;
    public int VarianceLineCount { get; set; }
    public decimal NetVarianceKg { get; set; }
    
    public List<StockTakeItemDto> StockTakeItems { get; set; } = new List<StockTakeItemDto>();
}

public class CreateStockTakeDto
{
    public int WarehouseId { get; set; }
    // Mặc định 0: trạng thái Draft được gán trong StockTakeMapping.ToEntity qua Lookup theo Code (không hard-code Id).
    public int StockTakeStatusId { get; set; }
    public string? STCode { get; set; } // Auto-generated if null
    public string? Note { get; set; }
    public int? CreatedBy { get; set; }

    /// <summary>
    /// Phạm vi tạo snapshot: WAREHOUSE / ZONE / COLUMN / LOT / SKU.
    /// Không lưu trực tiếp xuống StockTake; scope được giữ bằng các StockTakeItem đã sinh.
    /// </summary>
    public string? ScopeType { get; set; }
    public string? ZoneName { get; set; }
    public int? LocationId { get; set; }
    public int? PaddyLotId { get; set; }
    public int? ProductVariantId { get; set; }
    
    public List<CreateStockTakeItemDto> StockTakeItems { get; set; } = new List<CreateStockTakeItemDto>();
}

public class StockTakeSummaryDto
{
    public int DraftCount { get; set; }
    public int SubmittedCount { get; set; }
    public int VarianceLineCount { get; set; }
    public decimal NetAdjustmentKg { get; set; }
}

public class StockTakeThresholdDto
{
    public decimal SmallVariancePercent { get; set; }
    public decimal MediumVariancePercent { get; set; }
    public decimal SmallVarianceKg { get; set; }
    public decimal MediumVarianceKg { get; set; }
}

public class UpdateStockTakeDto
{
    public int Id { get; set; }
    public int StockTakeStatusId { get; set; }
    public string? Note { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int? ApprovedByUserId { get; set; }
    public int? UpdatedBy { get; set; }
    
    public List<UpdateStockTakeItemDto> StockTakeItems { get; set; } = new List<UpdateStockTakeItemDto>();
}
