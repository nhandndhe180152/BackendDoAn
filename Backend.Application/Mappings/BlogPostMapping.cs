using System;
using Backend.Application.DTOs.BlogPosts;
using Backend.Domain.Entities;
using Backend.Share.Extensions;

namespace Backend.Application.Mappings;

public static class BlogPostMapping
{
    public static BlogPost ToEntity(this CreateBlogPostDto obj)
    {

        var data = new BlogPost
        {
            CreatedBy = obj.CreatedBy,
            Description = obj.Description,
            AuthorId = (int)obj.CreatedBy,
            Title = obj.Title,
            Content = obj.Content,
            BlogPostCategoryId = obj.BlogPostCategoryId,
            BlogPostLayoutId = obj.BlogPostLayoutId,
            BlogPostStatusId = obj.BlogPostStatusId,
            CoverImageId = obj.CoverImageId,
            IsApproved = obj.IsApproved,
            PublicationDate = obj.PublicationDate ?? DateTime.Now,
            ApprovalDate = obj.ApprovalDate,
            IsShowOnHomePage = obj.IsShowOnHomePage,
            IsPopular = obj.IsPopular,
            SeoAlias = obj.Title.RemoveVietnamese().ToSEO(),
            CreatedDate = DateTime.Now,

        };
        return data;
    }

    public static BlogPost ToEntity(this UpdateBlogPostDto obj, BlogPost existData)
    {
        existData.Description = obj.Description;
        existData.LastModifiedDate = DateTime.Now;
        existData.Title = obj.Title;
        existData.Content = obj.Content;
        existData.BlogPostCategoryId = obj.BlogPostCategoryId;
        existData.BlogPostLayoutId = obj.BlogPostLayoutId;
        existData.BlogPostStatusId = obj.BlogPostStatusId;
        existData.CoverImageId = obj.CoverImageId;
        existData.SeoAlias = obj.Title.RemoveVietnamese().ToSEO();
        existData.IsApproved = obj.IsApproved;
        existData.IsShowOnHomePage = obj.IsShowOnHomePage;
        existData.IsPopular = obj.IsPopular;
        existData.ApprovalDate = obj.ApprovalDate;
        existData.PublicationDate = obj.PublicationDate ?? DateTime.Now;
        existData.LastModifiedDate = DateTime.Now;
        existData.UpdatedBy = obj.UpdatedBy;
        return existData;
    }

    public static BlogPostDetailDto ToDto(this BlogPost entity)
    {
        return new BlogPostDetailDto
        {
            Id = entity.Id,
            Title = entity.Title,
            SeoAlias = entity.SeoAlias,
            Description = entity.Description,
            Content = entity.Content,
            IsApproved = entity.IsApproved,
            ApprovalDate = entity.ApprovalDate,
            PublicationDate = entity.PublicationDate,
            IsShowOnHomePage = entity.IsShowOnHomePage,
            IsPopular = entity.IsPopular,
        };
    }
}
