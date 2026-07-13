using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Phiếu mua lúa thực tế: cân, chốt giá theo chất lượng, thanh toán/công nợ, sinh mã lô.
/// Là nguồn gốc cho InboundOrder khi nhập lúa.
/// </summary>
public class PaddyPurchaseReceipt : EntityAuditBase<int>
{
    public int? OrganizationId { get; set; }
    public string ReceiptCode { get; set; } = null!;
    public int? ScheduleId { get; set; }
    public int FarmerId { get; set; }
    public int? RiceVarietyId { get; set; }
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

    // Navigation
    public virtual Organization? Organization { get; set; }
    public virtual PaddyPurchaseSchedule? Schedule { get; set; }
    public virtual Farmer Farmer { get; set; } = null!;
    public virtual RiceVariety? RiceVariety { get; set; }
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual PaddyLot? PaddyLot { get; set; }
}
