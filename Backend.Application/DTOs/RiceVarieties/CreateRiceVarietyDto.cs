using System;

namespace Backend.Application.DTOs.RiceVarieties;

public class CreateRiceVarietyDto
{
    public int? OrganizationId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Season { get; set; }
    public decimal? DefaultYieldRate { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; } = true;
    public int? CreatedBy { get; set; }
}
