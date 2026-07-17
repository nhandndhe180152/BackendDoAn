using System;

namespace Backend.Application.DTOs.Dashboard;

public class DashboardQuery
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int? WarehouseId { get; set; }
    public int? RiceVarietyId { get; set; }
}

public class DashboardSummaryDto
{
    public InventorySummaryDto Inventory { get; set; } = new();
    public DebtSummaryDto Debt { get; set; } = new();
    public MillingSummaryDto Milling { get; set; } = new();
    public SalesSummaryDto Sales { get; set; } = new();
    public AlertsSummaryDto Alerts { get; set; } = new();
}

public class InventorySummaryDto
{
    public decimal OnHandKg { get; set; }
    public decimal ReservedKg { get; set; }
    public decimal QuarantinedKg { get; set; }
    public decimal AvailableKg { get; set; }
    public decimal PaddyKg { get; set; }
    public decimal RiceKg { get; set; }
    public decimal ByproductKg { get; set; }
}

public class DebtSummaryDto
{
    public decimal FarmerPayable { get; set; }
    public decimal CustomerReceivable { get; set; }
    public decimal OverduePayable { get; set; }
    public decimal OverdueReceivable { get; set; }
}

public class MillingSummaryDto
{
    public decimal PaddyConsumedKg { get; set; }
    public decimal RiceOutputKg { get; set; }
    public decimal ActualYieldRate { get; set; }
    public decimal LossKg { get; set; }
}

public class SalesSummaryDto
{
    public decimal GrossRevenue { get; set; }
    public decimal AmountCollected { get; set; }
    public decimal OutstandingAmount { get; set; }
    public int CompletedOrderCount { get; set; }
}

public class AlertsSummaryDto
{
    public int OpenCount { get; set; }
    public int CriticalCount { get; set; }
    public int QuarantinedLotCount { get; set; }
    public int InspectionOverdueLotCount { get; set; }
}
