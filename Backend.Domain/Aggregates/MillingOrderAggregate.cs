using System;
using System.Collections.Generic;

namespace Backend.Domain.Aggregates;

public class MillingOrderAggregate
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public string MillingCode { get; set; } = null!;
    public int StatusId { get; set; }
    public string? StatusName { get; set; }
    public int WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public string? Reason { get; set; }
    public int? SalesOrderId { get; set; }
    public decimal YieldRateUsed { get; set; }
    public decimal TotalRiceOutputKg { get; set; }
    public decimal ComputedPaddyKg { get; set; }
    public decimal? ByproductKg { get; set; }
    public decimal? LossKg { get; set; }
    public string? MachineRef { get; set; }
    public int? OperatorId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal? TotalCost { get; set; }
    public DateTime CreatedDate { get; set; }
}
