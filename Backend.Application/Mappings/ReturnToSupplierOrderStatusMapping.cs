using System;
using Backend.Application.DTOs.ReturnToSupplierOrderStatuses;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class ReturnToSupplierOrderStatusMapping
{
    public static ReturnToSupplierOrderStatus ToEntity(this CreateReturnToSupplierOrderStatusDto dto)
    {
        return new ReturnToSupplierOrderStatus
        {
            Name = dto.Name,
            Color = dto.Color,
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            IsDeleted = false,
        };
    }

    public static ReturnToSupplierOrderStatus ToEntity(this UpdateReturnToSupplierOrderStatusDto dto, ReturnToSupplierOrderStatus existData)
    {
        existData.Name = dto.Name;
        existData.Color = dto.Color;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }
}
