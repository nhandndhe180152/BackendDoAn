using System;
using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Location : EntityAuditBase<int>
{
    public int WarehouseId { get; set; }
    public string ZoneName { get; set; } = null!;
    public string? ShelfRow { get; set; }
    public string? ShelfLevel { get; set; }
    public string? SlotCode { get; set; }
    public decimal? MaxCapacity { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    // FE-13 / FE-16
    public decimal CurrentOccupancy { get; set; }
    public int? AllowedCategoryId { get; set; }
    public int Priority { get; set; }
    public bool IsQuarantine { get; set; }

    public int? CurrentProductVariantId { get; set; }
    public bool IsSingleTypeColumn { get; set; } = true;

    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual ProductCategory? AllowedCategory { get; set; }
    public virtual ProductVariant? CurrentProductVariant { get; set; }
}
