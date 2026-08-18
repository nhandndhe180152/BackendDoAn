using System;
using System.Collections.Generic;

namespace Backend.Application.DTOs.MillingOrders;

public class MillingOrderInputItemDto
{
    public int PaddyLotId { get; set; }
    public int? LocationId { get; set; }
    public decimal ConsumedWeightKg { get; set; }
    public decimal? ReservedWeightKg { get; set; }
    public string? Note { get; set; }
}

public class MillingOrderOutputItemDto
{
    public int ProductVariantId { get; set; }
    public int? LocationId { get; set; }
    /// <summary>RICE | BROKEN | BRAN | HUSK</summary>
    public string OutputType { get; set; } = null!;
    public decimal OutputWeightKg { get; set; }
    public int? BagCount { get; set; }
    public bool IsByproduct { get; set; }
    public decimal? UnitCost { get; set; }
}

public class CreateMillingOrderDto
{
    public int? OrganizationId { get; set; }
    public int WarehouseId { get; set; }
    public string? Reason { get; set; }
    public int? SalesOrderId { get; set; }
    public int? RiceVarietyId { get; set; }
    public decimal? MoisturePercent { get; set; }
    public decimal ExpectedYield { get; set; }
    public decimal TargetRiceKg { get; set; }
    public decimal? MillingCost { get; set; }
    public decimal? IncidentalCost { get; set; }
    public DateTime? ExpectedCompletionDate { get; set; }
    public int? CreatedBy { get; set; }
}
