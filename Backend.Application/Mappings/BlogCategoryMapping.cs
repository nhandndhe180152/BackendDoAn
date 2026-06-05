using System;
using Backend.Application.DTOs.BlogPostCategories;
using Backend.Domain.Entities;
using Backend.Share.Extensions;

namespace Backend.Application.Mappings;

public static class BlogCategoryMapping
{
    public static BlogPostCategory ToEntity(this CreateBlogPostCategoryDto dto)
    {
        return new BlogPostCategory
        {
            Color = dto.Color,
            Description = dto.Description,
            SeoAlias = dto.Name.RemoveVietnamese().ToSEO(),
            Name = dto.Name,
            CreatedDate = DateTime.Now,
            CreatedBy = dto.CreatedBy
        };
    }

    public static BlogPostCategory ToEntity(this UpdateBlogPostCategoryDto dto, BlogPostCategory existData)
    {
        existData.Color = dto.Color;
        existData.Description = dto.Description;
        existData.SeoAlias = dto.Name.RemoveVietnamese().ToSEO();
        existData.Name = dto.Name;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;

        return existData;
    }

    public static BlogPostCategoryDetailDto ToDto(this BlogPostCategory entity)
    {
        return new BlogPostCategoryDetailDto
        {
            Color = entity.Color,
            Description = entity.Description,
            SeoAlias = entity.SeoAlias,
            CreatedDate = entity.CreatedDate,
            Id = entity.Id,
            Name = entity.Name,
        };
    }

    public static BlogPostCategoryListDto ToListDto(this BlogPostCategory entity)
    {
        return new BlogPostCategoryListDto
        {
            Color = entity.Color,
            Description = entity.Description,
            SeoAlias = entity.SeoAlias,
            CreatedDate = entity.CreatedDate,
            Id = entity.Id,
            Name = entity.Name,
        };
    }
}
