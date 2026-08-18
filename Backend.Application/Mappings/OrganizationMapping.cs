using System;
using Backend.Application.DTOs.Organizations;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class OrganizationMapping
{
    public static Organization ToEntity(this CreateOrganizationDto obj)
    {
        return new Organization
        {
            Code = obj.Code.Trim(),
            Name = obj.Name.Trim(),
            Description = obj.Description?.Trim(),
            TaxCode = obj.TaxCode?.Trim(),
            Address = obj.Address?.Trim(),
            ContactEmail = obj.ContactEmail?.Trim(),
            ContactPhone = obj.ContactPhone?.Trim(),
            LogoFileId = obj.LogoFileId,
            SubscriptionPlan = obj.SubscriptionPlan?.Trim(),
            SubscriptionExpiry = obj.SubscriptionExpiry,
            IsActive = obj.IsActive,
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now
        };
    }

    public static Organization ToEntity(this UpdateOrganizationDto obj, Organization existData)
    {
        existData.Code = obj.Code.Trim();
        existData.Name = obj.Name.Trim();
        existData.Description = obj.Description?.Trim();
        existData.TaxCode = obj.TaxCode?.Trim();
        existData.Address = obj.Address?.Trim();
        existData.ContactEmail = obj.ContactEmail?.Trim();
        existData.ContactPhone = obj.ContactPhone?.Trim();
        existData.LogoFileId = obj.LogoFileId;
        existData.SubscriptionPlan = obj.SubscriptionPlan?.Trim();
        existData.SubscriptionExpiry = obj.SubscriptionExpiry;
        existData.IsActive = obj.IsActive;
        existData.UpdatedBy = obj.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }

    public static OrganizationDetailDto ToDto(this Organization entity)
    {
        return new OrganizationDetailDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            TaxCode = entity.TaxCode,
            Address = entity.Address,
            ContactEmail = entity.ContactEmail,
            ContactPhone = entity.ContactPhone,
            LogoFileId = entity.LogoFileId,
            SubscriptionPlan = entity.SubscriptionPlan,
            SubscriptionExpiry = entity.SubscriptionExpiry,
            IsActive = entity.IsActive,
            CreatedDate = entity.CreatedDate,
            LastModifiedDate = entity.LastModifiedDate
        };
    }
}
