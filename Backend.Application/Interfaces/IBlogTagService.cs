using System;
using Backend.Application.DTOs.BlogPostTags;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IBlogTagService : IServiceBase<int, CreateBlogPostTagDto, UpdateBlogPostTagDto, DTParameter>
{
}
