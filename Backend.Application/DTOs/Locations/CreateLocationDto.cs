using System;

namespace Backend.Application.DTOs.Locations;

public class CreateLocationDto
{
    public int WarehouseId { get; set; }
    public string ZoneName { get; set; } = null!;
    public string? ShelfRow { get; set; }
    public string? ShelfLevel { get; set; }
    public string? SlotCode { get; set; }
    public decimal? MaxCapacity { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public decimal CurrentOccupancy { get; set; }
    public int? AllowedCategoryId { get; set; }
    public int Priority { get; set; }
    public bool IsQuarantine { get; set; }
}
