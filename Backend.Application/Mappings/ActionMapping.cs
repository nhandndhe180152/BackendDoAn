using System;
using Backend.Application.DTOs.Actions;

namespace Backend.Application.Mappings;

public static class ActionMapping
{
    public static Domain.Entities.Action ToEntity(this CreateActionDto obj)
    {
        return new Domain.Entities.Action
        {
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now,
            Code = obj.Code,
            Description = obj.Description,
            Name = obj.Name,
        };
    }

    public static Domain.Entities.Action ToEntity(this UpdateActionDto obj, Domain.Entities.Action existData)
    {
        existData.UpdatedBy = obj.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        existData.Name = obj.Name;
        existData.Description = obj.Description;
        existData.Code = obj.Code;

        return existData;
    }

    public static ActionDetailDto ToDto(this Domain.Entities.Action obj)
    {
        return new ActionDetailDto
        {
            Id = obj.Id,
            Code = obj.Code,
            Name = obj.Name,
            Description = obj.Description,
            CreatedDate = obj.CreatedDate
        };
    }
}
