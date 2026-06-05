using System;
using Backend.Application.DTOs.PaymentMethods;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class PaymentMethodMapping
{
    public static PaymentMethod ToEntity(this CreatePaymentMethodDto obj)
    {
        return new PaymentMethod
        {
            CreatedBy = obj.CreatedBy,
            Description = obj.Description,
            Name = obj.Name,
            CreatedDate = DateTime.Now
        };
    }

    public static PaymentMethod ToEntity(this UpdatePaymentMethodDto obj, PaymentMethod existData)
    {
        existData.UpdatedBy = obj.UpdatedBy;
        existData.Description = obj.Description;
        existData.Name = obj.Name;
        existData.LastModifiedDate = DateTime.Now;

        return existData;
    }

    public static PaymentMethodDetailDto ToDto(this PaymentMethod entity)
    {
        return new PaymentMethodDetailDto
        {
            Id = entity.Id,
            CreatedDate = entity.CreatedDate,
            Description = entity.Description,
            Name = entity.Name,
        };
    }
}
