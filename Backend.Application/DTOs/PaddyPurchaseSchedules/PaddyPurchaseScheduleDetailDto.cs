using System;

namespace Backend.Application.DTOs.PaddyPurchaseSchedules;

public class PaddyPurchaseScheduleDetailDto
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public string ScheduleCode { get; set; } = null!;
    public int FarmerId { get; set; }
    public string? FarmerName { get; set; }
    public int StatusId { get; set; }
    public string? StatusName { get; set; }
    public int? RiceVarietyId { get; set; }
    public string? RiceVarietyName { get; set; }
    public DateTime ScheduleDate { get; set; }
    public string? Location { get; set; }
    public decimal? EstimatedQtyKg { get; set; }
    public decimal? ExpectedPrice { get; set; }
    public int? AssignedUserId { get; set; }
    public int? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}
