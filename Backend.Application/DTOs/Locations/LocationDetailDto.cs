using System;

namespace Backend.Application.DTOs.Locations;

public class LocationDetailDto
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public string ZoneName { get; set; } = null!;
    public string? ShelfRow { get; set; }
    public string? ShelfLevel { get; set; }
    public string? SlotCode { get; set; }
    public int? MaxCapacity { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
}
