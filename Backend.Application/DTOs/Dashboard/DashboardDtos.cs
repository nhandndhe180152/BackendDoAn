using System;
using System.Collections.Generic;

namespace Backend.Application.DTOs.Dashboard;

public class DashboardQuery
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int? WarehouseId { get; set; }
    public int? RiceVarietyId { get; set; }

    /// <summary>Mốc thời gian cho biểu đồ thu mua: "today" (7 ngày) | "month" (4 tuần) | "year" (12 tháng).</summary>
    public string? Period { get; set; }
}

public class DashboardSummaryDto
{
    public InventorySummaryDto Inventory { get; set; } = new();
    public DebtSummaryDto Debt { get; set; } = new();
    public MillingSummaryDto Milling { get; set; } = new();
    public SalesSummaryDto Sales { get; set; } = new();
    public AlertsSummaryDto Alerts { get; set; } = new();
    public EfficiencyMetricsDto Efficiency { get; set; } = new();
    public List<AlertItemDto> RecentAlerts { get; set; } = new();
}

public class InventorySummaryDto
{
    public decimal OnHandKg { get; set; }
    public decimal SellableOnHandKg { get; set; }
    public decimal ReservedKg { get; set; }
    public decimal QuarantinedKg { get; set; }
    public decimal OtherBlockedKg { get; set; }
    public decimal AvailableKg { get; set; }
    public decimal PaddyKg { get; set; }
    public decimal PaddyDeltaTodayKg { get; set; }
    public decimal RiceKg { get; set; }
    public decimal RiceDeltaTodayKg { get; set; }
    public decimal ByproductKg { get; set; }
}

public class DebtSummaryDto
{
    public decimal FarmerPayable { get; set; }
    public decimal CustomerReceivable { get; set; }
    public decimal OverduePayable { get; set; }
    public decimal OverdueReceivable { get; set; }
    public decimal TotalDebt => FarmerPayable + CustomerReceivable;
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
    public int PendingDeliveryCount { get; set; }
    public int PendingDeliveryActionRequiredCount { get; set; }
}

public class AlertsSummaryDto
{
    public int OpenCount { get; set; }
    public int CriticalCount { get; set; }
    public int QuarantinedLotCount { get; set; }
    public int InspectionOverdueLotCount { get; set; }
}

public class DashboardTaskDto
{
    public string Time { get; set; } = string.Empty; // "08:00"
    public string Type { get; set; } = string.Empty; // "PURCHASE", "DELIVERY", "INSPECTION"
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "Đã xác nhận", "Chờ xử lý"
}

public class ChartDataPointDto
{
    public string DayOfWeek { get; set; } = string.Empty; // "T2", "T3" ...
    public decimal VolumeTons { get; set; } // Sản lượng (tấn)
    public decimal AveragePrice { get; set; } // Giá (đ/kg)
}

public class EfficiencyMetricsDto
{
    public decimal OnTimeDeliveryRate { get; set; } // e.g. 94.2
    public decimal OnTimeDeliveryTarget { get; set; } = 95.0m;
    public decimal DebtRecoveryRate { get; set; } // e.g. 76.3
    public decimal WarehouseLossRate { get; set; } // e.g. 0.8
    public decimal WarehouseLossTarget { get; set; } = 1.0m;
}

public class AlertItemDto
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // "Warning", "Critical"
    public string TimeAgo { get; set; } = string.Empty; // "30p trước"
    public DateTime CreatedAt { get; set; }
}
