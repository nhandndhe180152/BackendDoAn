using System;
using Backend.Application.DTOs.BlogPostComments;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IBlogCommentService : IServiceBase<int, CreateBlogPostCommentDto, UpdateBlogPostCommentDto, DTParameter>
{
}
