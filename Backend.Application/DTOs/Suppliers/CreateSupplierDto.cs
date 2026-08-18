using System;

namespace Backend.Application.DTOs.Suppliers;

public class CreateSupplierDto
{
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxCode { get; set; }
    public bool IsActive { get; set; } = true;
    public int? CreatedBy { get; set; }
}
