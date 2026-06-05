using System;

namespace Backend.Application.DTOs.Warehouses;

public class CreateWarehouseDto
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Address { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int? CreatedBy { get; set; }
}
