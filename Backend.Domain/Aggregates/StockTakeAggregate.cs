using System;

namespace Backend.Domain.Aggregates;

public class StockTakeAggregate
{
    public int Id { get; set; }
    public string STCode { get; set; } = null!;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public int StockTakeStatusId { get; set; }
    public string? Note { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime CreatedDate { get; set; }
}
