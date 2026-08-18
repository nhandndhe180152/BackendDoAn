using System;

namespace Backend.Domain.Aggregates;

public class RiceVarietyAggregate
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Season { get; set; }
    public decimal? DefaultYieldRate { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
}
