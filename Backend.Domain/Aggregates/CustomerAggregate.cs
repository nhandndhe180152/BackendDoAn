using System;

namespace Backend.Domain.Aggregates;

public class CustomerAggregate
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? CustomerType { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxCode { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
}
