using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Supplier : EntityAuditBase<int>
{
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxCode { get; set; }
    public bool IsActive { get; set; }
}
