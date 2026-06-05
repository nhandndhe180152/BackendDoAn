using System;
using Backend.Application.DTOs.Warehouses;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class WarehouseMapping
{
    public static Warehouse ToEntity(this CreateWarehouseDto dto)
    {
        return new Warehouse
        {
            Code = dto.Code,
            Name = dto.Name,
            Address = dto.Address,
            Description = dto.Description,
            IsActive = dto.IsActive,
            CreatedDate = DateTime.Now,
            CreatedBy = dto.CreatedBy
        };
    }

    public static Warehouse ToEntity(this UpdateWarehouseDto dto, Warehouse existData)
    {
        existData.Code = dto.Code;
        existData.Name = dto.Name;
        existData.Address = dto.Address;
        existData.Description = dto.Description;
        existData.IsActive = dto.IsActive;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;

        return existData;
    }

    public static WarehouseDetailDto ToDto(this Warehouse entity)
    {
        return new WarehouseDetailDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            Address = entity.Address,
            Description = entity.Description,
            IsActive = entity.IsActive,
            CreatedDate = entity.CreatedDate
        };
    }

    public static WarehouseListDto ToListDto(this Warehouse entity)
    {
        return new WarehouseListDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            Address = entity.Address,
            Description = entity.Description,
            IsActive = entity.IsActive,
            CreatedDate = entity.CreatedDate
        };
    }
}
