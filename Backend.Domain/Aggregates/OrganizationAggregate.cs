using System;

namespace Backend.Domain.Aggregates;

public class OrganizationAggregate
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? TaxCode { get; set; }
    public string? Address { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public int? LogoFileId { get; set; }
    public string? SubscriptionPlan { get; set; }
    public DateTime? SubscriptionExpiry { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
}
