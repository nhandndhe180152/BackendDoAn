using System;

namespace Backend.Application.DTOs.PaddyPurchaseReceipts;

public class UpdatePaddyPurchaseReceiptDto
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public int? ScheduleId { get; set; }
    public int FarmerId { get; set; }
    public int? RiceVarietyId { get; set; }
    public int? ProductVariantId { get; set; }
    public int WarehouseId { get; set; }
    public decimal ActualWeightKg { get; set; }
    public int? BagCount { get; set; }
    public decimal AgreedPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal DebtAmount { get; set; }
    public string? QualityJson { get; set; }
    public string? PriceAdjustReason { get; set; }
    public DateTime ReceiptDate { get; set; }
    public int? UpdatedBy { get; set; }
}
