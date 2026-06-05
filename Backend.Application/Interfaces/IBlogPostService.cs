using System;
using Backend.Application.DTOs.BlogPosts;
using Backend.Domain.DTParameters;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IBlogPostService : IServiceBase<int, CreateBlogPostDto, UpdateBlogPostDto, BlogPostDTParameters>
{
    Task<ApiResponse> GetPagedClientAsync(BlogPostSearchDTParameters parameters);
    Task<ApiResponse> GetLatestArticleList();
}
