using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Location : EntityBase<int>
{
    public int WarehouseId { get; set; }
    public string ZoneName { get; set; } = null!;
    public string? ShelfRow { get; set; }
    public string? ShelfLevel { get; set; }
    public string? SlotCode { get; set; }
    public int? MaxCapacity { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
}
