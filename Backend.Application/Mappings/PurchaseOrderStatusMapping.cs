using System;
using Backend.Application.DTOs.PurchaseOrderStatuses;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class PurchaseOrderStatusMapping
{
    public static PurchaseOrderStatus ToEntity(this CreatePurchaseOrderStatusDto dto)
    {
        return new PurchaseOrderStatus
        {
            Name = dto.Name,
            Color = dto.Color,
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            IsDeleted = false,
        };
    }

    public static PurchaseOrderStatus ToEntity(this UpdatePurchaseOrderStatusDto dto, PurchaseOrderStatus existData)
    {
        existData.Name = dto.Name;
        existData.Color = dto.Color;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }
}
