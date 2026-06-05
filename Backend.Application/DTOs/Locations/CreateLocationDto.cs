using System;

namespace Backend.Application.DTOs.Locations;

public class CreateLocationDto
{
    public int WarehouseId { get; set; }
    public string ZoneName { get; set; } = null!;
    public string? ShelfRow { get; set; }
    public string? ShelfLevel { get; set; }
    public string? SlotCode { get; set; }
    public int? MaxCapacity { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
