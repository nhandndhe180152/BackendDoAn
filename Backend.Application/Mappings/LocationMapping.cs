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
            CreatedDate = entity.CreatedDate
        };
    }
}
