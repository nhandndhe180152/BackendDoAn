using System;

namespace Backend.Domain.Aggregates;

public class MillingYieldConfigAggregate
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public int? RiceVarietyId { get; set; }
    public string? RiceVarietyCode { get; set; }
    public string? RiceVarietyName { get; set; }
    public decimal? MoistureFrom { get; set; }
    public decimal? MoistureTo { get; set; }
    public decimal YieldRate { get; set; }
    public decimal? BrokenRiceRate { get; set; }
    public decimal? BranRate { get; set; }
    public decimal? HuskRate { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
}
