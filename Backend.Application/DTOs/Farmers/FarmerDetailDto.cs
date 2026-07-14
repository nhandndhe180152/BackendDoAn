using System;

namespace Backend.Application.DTOs.Farmers;

public class FarmerDetailDto
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Region { get; set; }
    public string? ReputationNote { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}
