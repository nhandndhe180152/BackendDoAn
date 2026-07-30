using System;
using System.Collections.Generic;

namespace Backend.Application.DTOs.Dashboard;

public class ReportPageDto<T>
{
    public List<T> DataSource { get; set; } = new();
    public int Total { get; set; }
    public int TotalFiltered { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalFiltered / (double)PageSize);
    public object? Summary { get; set; }
    public List<ReportChartPointDto> Chart { get; set; } = new();
}

public class ReportChartPointDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal? SecondaryValue { get; set; }
}

public class ReportOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
}

public class ReportFilterOptionsDto
{
    public List<ReportOptionDto> Warehouses { get; set; } = new();
    public List<ReportOptionDto> RiceVarieties { get; set; } = new();
    public List<ReportOptionDto> ProductVariants { get; set; } = new();
    public List<ReportOptionDto> PaddyLots { get; set; } = new();
    public List<ReportOptionDto> Locations { get; set; } = new();
    public List<ReportOptionDto> Farmers { get; set; } = new();
    public List<ReportOptionDto> Customers { get; set; } = new();
}

public class ReportOverviewDto
{
    public decimal PaddyOnHandKg { get; set; }
    public decimal RiceOnHandKg { get; set; }
    public decimal QuarantinedKg { get; set; }
    public int PendingDeliveryCount { get; set; }
    public decimal Revenue { get; set; }
    public decimal CustomerReceivable { get; set; }
    public decimal FarmerPayable { get; set; }
    public decimal RelativeProfit { get; set; }
    public int QualityAlertCount { get; set; }
    public int GoodSourceCount { get; set; }
    public int TotalSourceCount { get; set; }
    public List<string> TopOverdueDebts { get; set; } = new();
    public List<string> OperationalAlerts { get; set; } = new();
}

public class InventoryByLotReportDto
{
    public int LotId { get; set; }
    public string LotCode { get; set; } = null!;
    public string LotType { get; set; } = null!;
    public string? RiceVariety { get; set; }
    public string SKU { get; set; } = null!;
    public string ProductVariant { get; set; } = null!;
    public string Warehouse { get; set; } = null!;
    public string? Location { get; set; }
    public string? Locations { get; set; }
    public int? BagCount { get; set; }
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

public class DebtDocumentReportDto
{
    public string Direction { get; set; } = null!;
    public string PartyType { get; set; } = null!;
    public int PartyId { get; set; }
    public string PartyName { get; set; } = null!;
    public string? RefType { get; set; }
    public int? RefId { get; set; }
    public string DocumentCode { get; set; } = null!;
    public DateTime TransactionDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string Status { get; set; } = null!;
    public int DaysOverdue { get; set; }
}

public class PurchaseReportDto
{
    public int ReceiptId { get; set; }
    public string ReceiptCode { get; set; } = null!;
    public DateTime ReceiptDate { get; set; }
    public int FarmerId { get; set; }
    public string FarmerName { get; set; } = null!;
    public int? RiceVarietyId { get; set; }
    public string? RiceVarietyName { get; set; }
    public string WarehouseName { get; set; } = null!;
    public decimal WeightKg { get; set; }
    public int? BagCount { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal DebtAmount { get; set; }
    public string? QualitySummary { get; set; }
}

public class MillingYieldReportDto
{
    public int MillingOrderId { get; set; }
    public string MillingCode { get; set; } = null!;
    public string WarehouseName { get; set; } = null!;
    public decimal ComputedPaddyKg { get; set; }
    public decimal TotalRiceOutputKg { get; set; }
    public decimal ByproductKg { get; set; }
    public decimal ActualYieldRate { get; set; }
    public decimal LossKg { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class SalesRevenueReportDto
{
    public int SalesOrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public string SOCode { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public string Channel { get; set; } = null!;
    public string ItemSummary { get; set; } = null!;
    public decimal TotalQuantity { get; set; }
    public decimal TotalWeightKg { get; set; }
    public decimal TotalAmount { get; set; }       // GrossRevenue
    public decimal DepositAmount { get; set; }     // Tiền đặt cọc
    public decimal AmountCollected { get; set; }   // Tổng đã thu = Deposit + Payments
    public decimal OutstandingAmount { get; set; } // Còn lại phải thu = Total - Collected
    public string StatusName { get; set; } = null!;
}

public class QualityAlertReportDto
{
    public int LotId { get; set; }
    public string LotCode { get; set; } = null!;
    public string LotType { get; set; } = null!;
    public string ProductVariantName { get; set; } = null!;
    public string WarehouseName { get; set; } = null!;
    public string? Location { get; set; }
    public decimal RemainingWeightKg { get; set; }
    public decimal AffectedWeightKg { get; set; }
    public string? QualityStatus { get; set; }
    public string RiskSummary { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public string? Recommendation { get; set; }
    public DateTime? LastInspectionAt { get; set; }
    public bool IsQuarantined { get; set; }
    public bool IsInspectionOverdue { get; set; }
}

public class RelativeProfitReportDto
{
    public string PeriodLabel { get; set; } = null!;
    public DateTime PeriodStart { get; set; }
    public decimal Revenue { get; set; }
    public decimal PaddyCost { get; set; }
    public decimal MillingCost { get; set; }
    public decimal RelativeProfit { get; set; }
    public decimal MarginPercent { get; set; }
}

public class SourceEffectivenessReportDto
{
    public int FarmerId { get; set; }
    public string FarmerName { get; set; } = null!;
    public decimal PurchasedKg { get; set; }
    public decimal AveragePurchasePrice { get; set; }
    public int RiskLotCount { get; set; }
    public decimal RelatedRevenue { get; set; }
    public decimal RelativeProfit { get; set; }
    public string Assessment { get; set; } = null!;
}

public class ReportExportDto
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = "report.xlsx";
}
