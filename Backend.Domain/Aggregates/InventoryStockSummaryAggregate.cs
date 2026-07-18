namespace Backend.Domain.Aggregates;

/// <summary>
/// Kết quả tổng hợp tồn kho theo trạng thái (repository-level) cho 5 thẻ KPI
/// màn giám sát tồn kho. Application map sang InventoryStockSummaryDto.
/// </summary>
public class InventoryStockSummaryAggregate
{
    public decimal TotalOnHand { get; set; }
    public decimal TotalAvailable { get; set; }
    public decimal TotalReserved { get; set; }
    public decimal TotalProcessing { get; set; }
    public decimal TotalQuarantine { get; set; }

    public decimal TotalOnHandWeightKg { get; set; }
    public decimal TotalAvailableWeightKg { get; set; }
    public decimal TotalReservedWeightKg { get; set; }
    public decimal TotalProcessingWeightKg { get; set; }
    public decimal TotalQuarantineWeightKg { get; set; }

    public int LineCount { get; set; }
    public int QuarantineLotCount { get; set; }
    public int LowStockCount { get; set; }
}
