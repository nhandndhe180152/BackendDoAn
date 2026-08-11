using System;
using System.Collections.Generic;

namespace Backend.Application.DTOs.MillingOrders;

public class MillingOrderDetailDto
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public string MillingCode { get; set; } = null!;
    public int StatusId { get; set; }
    public string? StatusName { get; set; }
    public string? StatusCode { get; set; }
    public int WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public int? RiceVarietyId { get; set; }
    public string? RiceVarietyName { get; set; }
    public string? Reason { get; set; }
    public int? SalesOrderId { get; set; }
    public decimal YieldRateUsed { get; set; }
    public decimal TotalRiceOutputKg { get; set; }
    public decimal ComputedPaddyKg { get; set; }
    public decimal? ActualPaddyInputKg { get; set; }
    public decimal? ActualYieldRate { get; set; }
    public decimal? ByproductKg { get; set; }
    public decimal? LossKg { get; set; }
    public string? MachineRef { get; set; }
    public int? OperatorId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal? TotalCost { get; set; }
    /// <summary>
    /// Chi phí vận hành đã khai báo trước khi hoàn tất. Schema hiện tại chỉ
    /// có TotalCost nên giá trị này là tổng chi phí xay + phát sinh.
    /// </summary>
    public decimal? MillingCost { get; set; }
    public decimal? IncidentalCost { get; set; }
    public List<MillingOrderInputDetailDto> Inputs { get; set; } = new();
    public List<MillingOrderOutputDetailDto> Outputs { get; set; } = new();
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}

public class MillingOrderInputDetailDto
{
    public int Id { get; set; }
    public int PaddyLotId { get; set; }
    public string? LotCode { get; set; }
    public int? LocationId { get; set; }
    public string? LocationCode { get; set; }
    public decimal ConsumedWeightKg { get; set; }
    public decimal? ReservedWeightKg { get; set; }
    public string? Note { get; set; }
    public List<MillingSelectedBagDto> Bags { get; set; } = new();
}

public class MillingSelectedBagDto
{
    public int BagId { get; set; }
    public int BagNo { get; set; }
    public decimal WeightKg { get; set; }
    public int StackOrder { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class MillingOrderOutputDetailDto
{
    public int Id { get; set; }
    public int ProductVariantId { get; set; }
    public string? SKU { get; set; }
    public int? OutputLotId { get; set; }
    public int? LocationId { get; set; }
    public string? OutputType { get; set; }
    public decimal OutputWeightKg { get; set; }
    public int? BagCount { get; set; }
    public bool IsByproduct { get; set; }
    public decimal? UnitCost { get; set; }
}
