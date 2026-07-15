using System;

namespace Backend.Application.DTOs.MillingOrders;

public class UpdateMillingOrderDto
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public int WarehouseId { get; set; }
    public string? Reason { get; set; }
    public int? SalesOrderId { get; set; }
    public decimal YieldRateUsed { get; set; }
    public decimal TotalRiceOutputKg { get; set; }
    public decimal? ByproductKg { get; set; }
    public decimal? LossKg { get; set; }
    public string? MachineRef { get; set; }
    public int? OperatorId { get; set; }
    public DateTime? StartedAt { get; set; }
    public int? UpdatedBy { get; set; }
}
