using System;

namespace Backend.Domain.Aggregates;

public class PaddyPurchaseScheduleAggregate
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public string ScheduleCode { get; set; } = null!;
    public int FarmerId { get; set; }
    public string? FarmerName { get; set; }  // Farmer.Name
    public int StatusId { get; set; }
    public string? StatusName { get; set; }
    public int? RiceVarietyId { get; set; }
    public string? RiceVarietyName { get; set; }
    public DateTime ScheduleDate { get; set; }
    public string? Location { get; set; }
    public decimal? EstimatedQtyKg { get; set; }
    public decimal? ExpectedPrice { get; set; }
    public int? AssignedUserId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedDate { get; set; }
}
