using System;
using Backend.Application.DTOs.BlogPostCategories;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IBlogCategoryService : IServiceBase<int, CreateBlogPostCategoryDto, UpdateBlogPostCategoryDto, DTParameter>
{
}
