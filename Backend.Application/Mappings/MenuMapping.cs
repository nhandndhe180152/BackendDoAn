using System;
using Backend.Application.DTOs.Menus;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class MenuMapping
{
    public static Menu ToEntity(this CreateMenuDto dto)
    {
        return new Menu
        {
            ClassName = dto.ClassName,
            CreatedBy = dto.CreatedBy,
            CreatedDate = DateTime.Now,
            Icon = dto.Icon,
            MenuType = dto.MenuType,
            Name = dto.Name,
            ParentId = dto.ParentId != 0 ? dto.ParentId : null,
            SortOrder = dto.SortOrder,
            Url = dto.Url,
            TreeIds = string.Empty,
            IsAdminOnly = dto.IsAdminOnly,
        };
    }

    public static Menu ToEntity(this UpdateMenuDto dto, Menu existData)
    {
        existData.ClassName = dto.ClassName;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;
        existData.Icon = dto.Icon;
        existData.ParentId = dto.ParentId != 0 ? dto.ParentId : null;
        existData.MenuType = dto.MenuType;
        existData.Name = dto.Name;
        existData.SortOrder = dto.SortOrder;
        existData.Url = dto.Url;
        existData.IsAdminOnly = dto.IsAdminOnly;
        return existData;
    }
}
