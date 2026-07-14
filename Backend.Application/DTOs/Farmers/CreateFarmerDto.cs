using System;

namespace Backend.Application.DTOs.Farmers;

public class CreateFarmerDto
{
    public int? OrganizationId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Region { get; set; }
    public string? ReputationNote { get; set; }
    public bool IsActive { get; set; } = true;
    public int? CreatedBy { get; set; }
}
