using System;
using Backend.Application.DTOs.UnitOfMeasures;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class UnitOfMeasureMapping
{
    public static UnitOfMeasure ToEntity(this CreateUnitOfMeasureDto obj)
    {
        return new UnitOfMeasure
        {
            Name = obj.Name.Trim(),
            Symbol = obj.Symbol.Trim(),
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now
        };
    }

    public static UnitOfMeasure ToEntity(this UpdateUnitOfMeasureDto obj, UnitOfMeasure existData)
    {
        existData.Name = obj.Name.Trim();
        existData.Symbol = obj.Symbol.Trim();
        existData.UpdatedBy = obj.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        return existData;
    }

    public static UnitOfMeasureDetailDto ToDto(this UnitOfMeasure entity)
    {
        return new UnitOfMeasureDetailDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Symbol = entity.Symbol,
            CreatedDate = entity.CreatedDate,
            LastModifiedDate = entity.LastModifiedDate
        };
    }
}
