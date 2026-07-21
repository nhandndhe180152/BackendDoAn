using System;

namespace Backend.Application.DTOs.Dashboard;

public class InventoryByLotReportDto
{
    public string LotCode { get; set; } = null!;
    public string LotType { get; set; } = null!;
    public string? RiceVariety { get; set; }
    public string ProductVariant { get; set; } = null!;
    public string Warehouse { get; set; } = null!;
    public string? Location { get; set; }
    public string? Locations { get; set; }
    public DateTime InboundDate { get; set; }
    public decimal InitialWeightKg { get; set; }
    public decimal RemainingWeightKg { get; set; }
    public decimal OnHandKg { get; set; }
    public decimal ReservedKg { get; set; }
    public decimal QuarantinedKg { get; set; }
    public decimal AvailableKg { get; set; }
    public bool IsQuarantined { get; set; }
    public string QuarantineSource { get; set; } = null!;
    public DateTime? LastInspectionAt { get; set; }
    public string LotStatus { get; set; } = null!;
}

public class InventoryByWarehouseReportDto
{
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public decimal OnHandKg { get; set; }
    public decimal SellableOnHandKg { get; set; }
    public decimal ReservedKg { get; set; }
    public decimal QuarantinedKg { get; set; }
    public decimal AvailableKg { get; set; }
    public decimal PaddyKg { get; set; }
    public decimal RiceKg { get; set; }
    public decimal ByproductKg { get; set; }
    public int QuarantinedLotCount { get; set; }
    public int QuarantineLocationCount { get; set; }
}

public class InventoryByProductVariantReportDto
{
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public int ProductVariantId { get; set; }
    public string SKU { get; set; } = null!;
    public string ProductVariantName { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public decimal OnHandKg { get; set; }
    public decimal SellableOnHandKg { get; set; }
    public decimal ReservedKg { get; set; }
    public decimal QuarantinedKg { get; set; }
    public decimal AvailableKg { get; set; }
    public int QuarantinedLotCount { get; set; }
    public decimal QuarantineRatio { get; set; }
}

public class TwoWayDebtReportDto
{
    public string Direction { get; set; } = null!; // PAYABLE | RECEIVABLE
    public string PartyType { get; set; } = null!; // FARMER | CUSTOMER
    public int PartyId { get; set; }
    public string PartyName { get; set; } = null!;
    public decimal CurrentBalance { get; set; }
    public decimal OverdueAmount { get; set; }
}

public class MillingYieldReportDto
{
    public string MillingCode { get; set; } = null!;
    public string WarehouseName { get; set; } = null!;
    public decimal ComputedPaddyKg { get; set; }
    public decimal TotalRiceOutputKg { get; set; }
    public decimal ActualYieldRate { get; set; }
    public decimal LossKg { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class SalesRevenueReportDto
{
    public DateTime OrderDate { get; set; }
    public string SOCode { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public decimal TotalAmount { get; set; }       // GrossRevenue
    public decimal DepositAmount { get; set; }     // Tiền đặt cọc
    public decimal AmountCollected { get; set; }   // Tổng đã thu = Deposit + Payments
    public decimal OutstandingAmount { get; set; } // Còn lại phải thu = Total - Collected
    public string StatusName { get; set; } = null!;
}

public class QualityAlertReportDto
{
    public string LotCode { get; set; } = null!;
    public string ProductVariantName { get; set; } = null!;
    public string WarehouseName { get; set; } = null!;
    public decimal RemainingWeightKg { get; set; }
    public string? QualityStatus { get; set; }
    public DateTime? LastInspectionAt { get; set; }
    public bool IsQuarantined { get; set; }
    public bool IsInspectionOverdue { get; set; }
}
