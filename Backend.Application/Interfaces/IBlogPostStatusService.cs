using System;
using Backend.Application.DTOs.BlogPostStatuses;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IBlogPostStatusService : IServiceBase<int, CreateBlogPostStatusDto, UpdateBlogPostStatusDto, DTParameter>
{
}
