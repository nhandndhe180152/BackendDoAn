using System;
using System.Collections.Generic;
using Backend.Application.DTOs.InboundOrders;

namespace Backend.Application.DTOs.PaddyPurchaseReceipts;

public class PaddyPurchaseReceiptDetailDto
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public string ReceiptCode { get; set; } = null!;
    public int? ScheduleId { get; set; }
    public string? ScheduleCode { get; set; }
    public int FarmerId { get; set; }
    public string? FarmerName { get; set; }
    public int? RiceVarietyId { get; set; }
    public string? RiceVarietyName { get; set; }
    public int? ProductVariantId { get; set; }
    public string? ProductVariantName { get; set; }
    public string? ProductVariantSku { get; set; }
    public int WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public decimal ActualWeightKg { get; set; }
    public decimal? AcceptedWeightKg { get; set; }
    public decimal? RejectedWeightKg { get; set; }
    public int? BagCount { get; set; }
    public List<CreateBagDto> Bags { get; set; } = new();
    public decimal AgreedPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal DebtAmount { get; set; }
    public decimal RefundReceivableAmount { get; set; }
    public DateTime? DebtDueDate { get; set; }
    public DateTime? QcFinalizedAt { get; set; }
    public int? QcFinalizedBy { get; set; }
    public string? QualityJson { get; set; }
    public string? PriceAdjustReason { get; set; }
    public DateTime ReceiptDate { get; set; }
    public int? PaddyLotId { get; set; }
    public bool IsConfirmed { get; set; }
    public decimal StoredWeightKg { get; set; }
    public decimal RemainingWeightKg { get; set; }
    public bool IsFullyStored { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}
