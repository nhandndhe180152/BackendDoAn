using System;
using Backend.Application.DTOs.SalesOrderStatuses;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class SalesOrderStatusMapping
{
    public static SalesOrderStatus ToEntity(this CreateSalesOrderStatusDto dto)
    {
        return new SalesOrderStatus
        {
            Name = dto.Name,
            Color = dto.Color,
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            IsDeleted = false,
        };
    }

    public static SalesOrderStatus ToEntity(this UpdateSalesOrderStatusDto dto, SalesOrderStatus existData)
    {
        existData.Name = dto.Name;
        existData.Color = dto.Color;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }
}
