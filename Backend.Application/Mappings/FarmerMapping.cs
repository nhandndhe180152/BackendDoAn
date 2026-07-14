using System;
using Backend.Application.DTOs.Farmers;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class FarmerMapping
{
    public static Farmer ToEntity(this CreateFarmerDto obj)
    {
        return new Farmer
        {
            OrganizationId = obj.OrganizationId,
            Code = obj.Code.Trim(),
            Name = obj.Name.Trim(),
            Phone = obj.Phone?.Trim(),
            Address = obj.Address?.Trim(),
            Region = obj.Region?.Trim(),
            ReputationNote = obj.ReputationNote?.Trim(),
            IsActive = obj.IsActive,
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now
        };
    }

    public static Farmer ToEntity(this UpdateFarmerDto obj, Farmer existData)
    {
        existData.OrganizationId = obj.OrganizationId;
        existData.Code = obj.Code.Trim();
        existData.Name = obj.Name.Trim();
        existData.Phone = obj.Phone?.Trim();
        existData.Address = obj.Address?.Trim();
        existData.Region = obj.Region?.Trim();
        existData.ReputationNote = obj.ReputationNote?.Trim();
        existData.IsActive = obj.IsActive;
        existData.UpdatedBy = obj.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }

    public static FarmerDetailDto ToDto(this Farmer entity)
    {
        return new FarmerDetailDto
        {
            Id = entity.Id,
            OrganizationId = entity.OrganizationId,
            Code = entity.Code,
            Name = entity.Name,
            Phone = entity.Phone,
            Address = entity.Address,
            Region = entity.Region,
            ReputationNote = entity.ReputationNote,
            IsActive = entity.IsActive,
            CreatedDate = entity.CreatedDate,
            LastModifiedDate = entity.LastModifiedDate
        };
    }
}
