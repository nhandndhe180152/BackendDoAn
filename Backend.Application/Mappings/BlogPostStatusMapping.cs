using System;
using Backend.Application.DTOs.BlogPostStatuses;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class BlogPostStatusMapping
{
    public static BlogPostStatus ToEntity(this CreateBlogPostStatusDto dto)
    {
        return new BlogPostStatus
        {
            Color = dto.Color,
            Description = dto.Description,
            Name = dto.Name,
            CreatedDate = DateTime.Now,
            CreatedBy = dto.CreatedBy
        };
    }

    public static BlogPostStatus ToEntity(this UpdateBlogPostStatusDto dto, BlogPostStatus existData)
    {
        existData.Color = dto.Color;
        existData.Description = dto.Description;
        existData.Name = dto.Name;
        existData.UpdatedBy = dto.UpdatedBy;
        existData.LastModifiedDate = DateTime.Now;

        return existData;
    }

    public static BlogPostStatusDetailDto ToDto(this BlogPostStatus entity)
    {
        return new BlogPostStatusDetailDto
        {
            Color = entity.Color,
            Description = entity.Description,
            CreatedDate = entity.CreatedDate,
            Id = entity.Id,
            Name = entity.Name,
        };
    }

    public static BlogPostStatusListDto ToListDto(this BlogPostStatus entity)
    {
        return new BlogPostStatusListDto
        {
            Color = entity.Color,
            Description = entity.Description,
            CreatedDate = entity.CreatedDate,
            Id = entity.Id,
            Name = entity.Name,
        };
    }
}
