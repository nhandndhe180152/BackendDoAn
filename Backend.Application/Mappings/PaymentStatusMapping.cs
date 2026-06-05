using System;
using Backend.Application.DTOs.PaymentStatuses;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class PaymentStatusMapping
{
    public static PaymentStatus ToEntity(this CreatePaymentStatusDto dto)
    {
        return new PaymentStatus
        {
            Name = dto.Name,
            Description = dto.Description,
            Color = dto.Color,
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            IsDeleted = false,
        };
    }
    public static PaymentStatus ToEntity(this UpdatePaymentStatusDto dto, PaymentStatus existData)
    {
        existData.Color = dto.Color;
        existData.Name = dto.Name;
        existData.Description = dto.Description;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }
}
