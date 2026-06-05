using System;
using Backend.Application.DTOs.BlogPostTags;
using Backend.Application.Interfaces;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;

namespace Backend.Application.Implements;

public class BlogTagService : IBlogTagService
{
    private readonly IBlogTagRepository _blogTagRepository;

    public BlogTagService(IBlogTagRepository blogTagRepository)
    {
        _blogTagRepository = blogTagRepository;
    }

    public Task<ApiResponse> CreateAsync(CreateBlogPostTagDto obj)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateBlogPostTagDto> objs)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetAllAsync()
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetByIdAsync(int id)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> SoftDeleteAsync(int id)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> UpdateAsync(UpdateBlogPostTagDto obj)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateBlogPostTagDto> obj)
    {
        throw new NotImplementedException();
    }
}
