using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Lệnh xay xát — chứng từ chuyển đổi lúa → gạo + phụ phẩm.
/// Lấy lúa trực tiếp từ cột (không cân đầu vào).
/// kg lúa tiêu hao = TotalRiceOutputKg ÷ YieldRateUsed.
/// </summary>
public class MillingOrder : EntityAuditBase<int>
{
    public int? OrganizationId { get; set; }
    public string MillingCode { get; set; } = null!;
    public int StatusId { get; set; }
    public int WarehouseId { get; set; }
    /// <summary>Giống lúa được đóng dấu tại thời điểm tạo lệnh.</summary>
    public int? RiceVarietyId { get; set; }
    public string? Reason { get; set; }
    public int? SalesOrderId { get; set; }
    public decimal YieldRateUsed { get; set; }
    public decimal TotalRiceOutputKg { get; set; }

    /// <summary>kg lúa tiêu hao = TotalRiceOutputKg ÷ YieldRateUsed</summary>
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

    // Navigation
    public virtual Organization? Organization { get; set; }
    public virtual MillingOrderStatus Status { get; set; } = null!;
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual RiceVariety? RiceVariety { get; set; }
    public virtual SalesOrder? SalesOrder { get; set; }
    public virtual User? Operator { get; set; }
    public virtual ICollection<MillingOrderInput> MillingOrderInputs { get; set; } = new List<MillingOrderInput>();
    public virtual ICollection<MillingOrderOutput> MillingOrderOutputs { get; set; } = new List<MillingOrderOutput>();
}
