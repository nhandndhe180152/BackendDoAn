using System;
using System.Collections.Generic;

namespace Backend.Domain.Aggregates;

public class StockTransferAggregate
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public string TransferCode { get; set; } = null!;
    public int StatusId { get; set; }
    public string? StatusName { get; set; }
    public string? StatusColor { get; set; }
    public int FromWarehouseId { get; set; }
    public string? FromWarehouseName { get; set; }
    public int ToWarehouseId { get; set; }
    public string? ToWarehouseName { get; set; }
    public int? AssignedUserId { get; set; }
    public DateTime? TransferDate { get; set; }
    public string? Note { get; set; }
    public int ItemCount { get; set; }
    public decimal TotalWeightKg { get; set; }
    public string? ItemDisplay { get; set; }
    public DateTime CreatedDate { get; set; }
}
