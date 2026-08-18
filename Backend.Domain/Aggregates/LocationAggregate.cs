using System;

namespace Backend.Domain.Aggregates;

public class LocationAggregate
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public string ZoneName { get; set; } = null!;
    public string? ShelfRow { get; set; }
    public string? ShelfLevel { get; set; }
    public string? SlotCode { get; set; }
    public decimal? MaxCapacity { get; set; }
    public decimal CurrentOccupancy { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsOutboundStaging { get; set; }
    public int? OutboundLockOrderId { get; set; }
    public DateTime CreatedDate { get; set; }
}
