using System;

namespace Backend.Application.DTOs.PaddyPurchaseSchedules;

public class UpdatePaddyPurchaseScheduleDto
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
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
    public int? UpdatedBy { get; set; }
}
