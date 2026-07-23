using System;
using System.Collections.Generic;

namespace Backend.Application.DTOs.MillingOrders;

public class CompleteMillingOrderDto
{
    public List<MillingOrderOutputItemDto> Outputs { get; set; } = new();
    public decimal? LossKg { get; set; }
    public decimal? ByproductKg { get; set; }
    public decimal ActualYieldRate { get; set; }
    public string? MachineRef { get; set; }
    public int? OperatorId { get; set; }
    public string? Note { get; set; }
    public int? UpdatedBy { get; set; }
}
