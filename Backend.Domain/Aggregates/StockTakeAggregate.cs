using System;

namespace Backend.Domain.Aggregates;

public class StockTakeAggregate
{
    public int Id { get; set; }
    public string STCode { get; set; } = null!;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public int StockTakeStatusId { get; set; }
    public string StockTakeStatusCode { get; set; } = null!;
    public string StockTakeStatusName { get; set; } = null!;
    public string? StockTakeStatusColor { get; set; }
    public string? Note { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int? ApprovedByUserId { get; set; }
    public string? ApproveNote { get; set; }
    public DateTime CreatedDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public string ScopeDisplay { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public int VarianceLineCount { get; set; }
    public decimal NetVarianceKg { get; set; }
}
