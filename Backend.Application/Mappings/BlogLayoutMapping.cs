using System;
using Backend.Application.DTOs.BlogPostLayouts;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class BlogLayoutMapping
{
    public static BlogPostLayout ToEntity(this CreateBlogPostLayoutDto dto)
    {
        return new BlogPostLayout
        {
            Description = dto.Description,
            Name = dto.Name,
            CreatedDate = DateTime.Now,
            CreatedBy = dto.CreatedBy
        };
    }

    public static BlogPostLayout ToEntity(this UpdateBlogPostLayoutDto dto, BlogPostLayout existData)
    {
        existData.Description = dto.Description;
        existData.Name = dto.Name;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;

        return existData;
    }

    public static BlogPostLayoutDetailDto ToDto(this BlogPostLayout entity)
    {
        return new BlogPostLayoutDetailDto
        {
            Description = entity.Description,
            CreatedDate = entity.CreatedDate,
            Id = entity.Id,
            Name = entity.Name,
        };
    }

    public static BlogPostLayoutListDto ToListDto(this BlogPostLayout entity)
    {
        return new BlogPostLayoutListDto
        {
            Description = entity.Description,
            CreatedDate = entity.CreatedDate,
            Id = entity.Id,
            Name = entity.Name,
        };
    }
}
