using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Lịch hẹn đi thu lúa từ nông dân.
/// Một lịch có thể sinh nhiều phiếu mua.
/// </summary>
public class PaddyPurchaseSchedule : EntityAuditBase<int>
{
    public int? OrganizationId { get; set; }
    public string ScheduleCode { get; set; } = null!;
    public int FarmerId { get; set; }
    public int StatusId { get; set; }
    public int? RiceVarietyId { get; set; }
    public DateTime ScheduleDate { get; set; }
    public string? Location { get; set; }
    public decimal? EstimatedQtyKg { get; set; }
    public decimal? ExpectedPrice { get; set; }
    public int? AssignedUserId { get; set; }
    public int? WarehouseId { get; set; }
    public string? Note { get; set; }

    // Navigation
    public virtual Organization? Organization { get; set; }
    public virtual Farmer Farmer { get; set; } = null!;
    public virtual PaddyPurchaseScheduleStatus Status { get; set; } = null!;
    public virtual RiceVariety? RiceVariety { get; set; }
    public virtual User? AssignedUser { get; set; }
    public virtual Warehouse? Warehouse { get; set; }
    public virtual ICollection<PaddyPurchaseReceipt> PaddyPurchaseReceipts { get; set; } = new List<PaddyPurchaseReceipt>();
}
