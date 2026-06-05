using System;
using Backend.Application.DTOs.BlogPostLayouts;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IBlogLayoutService : IServiceBase<int, CreateBlogPostLayoutDto, UpdateBlogPostLayoutDto, DTParameter>
{
}
