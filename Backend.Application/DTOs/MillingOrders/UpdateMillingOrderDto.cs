using System;

namespace Backend.Application.DTOs.MillingOrders;

public class UpdateMillingOrderDto
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public int WarehouseId { get; set; }
    public string? Reason { get; set; }
    public int? SalesOrderId { get; set; }
    public decimal ExpectedYield { get; set; }
    public decimal TargetRiceKg { get; set; }
    public DateTime? ExpectedCompletionDate { get; set; }
    public int? UpdatedBy { get; set; }
}
