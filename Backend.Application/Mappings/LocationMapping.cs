using System;
using Backend.Application.DTOs.Locations;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class LocationMapping
{
    public static Location ToEntity(this CreateLocationDto dto)
    {
        return new Location
        {
            WarehouseId = dto.WarehouseId,
            ZoneName = dto.ZoneName,
            ShelfRow = dto.ShelfRow,
            ShelfLevel = dto.ShelfLevel,
            SlotCode = dto.SlotCode,
            MaxCapacity = dto.MaxCapacity,
            Description = dto.Description,
            IsActive = dto.IsActive,
            CurrentOccupancy = dto.CurrentOccupancy,
            AllowedCategoryId = dto.AllowedCategoryId,
            Priority = dto.Priority,
            IsQuarantine = dto.IsQuarantine,
            CreatedDate = DateTime.Now
        };
    }

    public static Location ToEntity(this UpdateLocationDto dto, Location existData)
    {
        existData.WarehouseId = dto.WarehouseId;
        existData.ZoneName = dto.ZoneName;
        existData.ShelfRow = dto.ShelfRow;
        existData.ShelfLevel = dto.ShelfLevel;
        existData.SlotCode = dto.SlotCode;
        existData.MaxCapacity = dto.MaxCapacity;
        existData.Description = dto.Description;
        existData.IsActive = dto.IsActive;
        existData.CurrentOccupancy = dto.CurrentOccupancy;
        existData.AllowedCategoryId = dto.AllowedCategoryId;
        existData.Priority = dto.Priority;
        existData.IsQuarantine = dto.IsQuarantine;

        return existData;
    }

    public static LocationDetailDto ToDto(this Location entity)
    {
        return new LocationDetailDto
        {
            Id = entity.Id,
            WarehouseId = entity.WarehouseId,
            WarehouseName = entity.Warehouse?.Name ?? string.Empty,
            ZoneName = entity.ZoneName,
            ShelfRow = entity.ShelfRow,
            ShelfLevel = entity.ShelfLevel,
            SlotCode = entity.SlotCode,
            MaxCapacity = entity.MaxCapacity,
            Description = entity.Description,
            IsActive = entity.IsActive,
            CurrentOccupancy = entity.CurrentOccupancy,
            AllowedCategoryId = entity.AllowedCategoryId,
            AllowedCategoryName = entity.AllowedCategory?.Name,
            Priority = entity.Priority,
            IsQuarantine = entity.IsQuarantine,
            IsOutboundStaging = entity.IsOutboundStaging,
            OutboundLockOrderId = entity.OutboundLockOrderId,
            OutboundLockOrderCode = entity.OutboundLockOrder?.SalesOrder?.SOCode,
            OutboundLockedAt = entity.OutboundLockedAt,
            CurrentProductVariantId = entity.CurrentProductVariantId,
            CreatedDate = entity.CreatedDate
        };
    }

    public static LocationListDto ToListDto(this Location entity)
    {
        return new LocationListDto
        {
            Id = entity.Id,
            WarehouseId = entity.WarehouseId,
            WarehouseName = entity.Warehouse?.Name ?? string.Empty,
            ZoneName = entity.ZoneName,
            ShelfRow = entity.ShelfRow,
            ShelfLevel = entity.ShelfLevel,
            SlotCode = entity.SlotCode,
            MaxCapacity = entity.MaxCapacity,
            Description = entity.Description,
            IsActive = entity.IsActive,
            CurrentOccupancy = entity.CurrentOccupancy,
            AllowedCategoryId = entity.AllowedCategoryId,
            AllowedCategoryName = entity.AllowedCategory?.Name,
            Priority = entity.Priority,
            IsQuarantine = entity.IsQuarantine,
            IsOutboundStaging = entity.IsOutboundStaging,
            OutboundLockOrderId = entity.OutboundLockOrderId,
            OutboundLockOrderCode = entity.OutboundLockOrder?.SalesOrder?.SOCode,
            OutboundLockedAt = entity.OutboundLockedAt,
            CurrentProductVariantId = entity.CurrentProductVariantId,
            CreatedDate = entity.CreatedDate
        };
    }
}
