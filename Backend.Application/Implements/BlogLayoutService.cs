using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.BlogPostLayouts;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class BlogLayoutService : IBlogLayoutService
{
    private readonly IBlogLayoutRepository _blogLayoutRepository;

    public BlogLayoutService(IBlogLayoutRepository blogLayoutRepository)
    {
        _blogLayoutRepository = blogLayoutRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateBlogPostLayoutDto obj)
    {
        var isExistName = await _blogLayoutRepository.AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == obj.Name.ToLower());
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        var model = obj.ToEntity();

        await _blogLayoutRepository.CreateAsync(model);
        await _blogLayoutRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id);
    }

    public async Task<ApiResponse> CreateListAsync(IEnumerable<CreateBlogPostLayoutDto> objs)
    {
        var models = objs.Select(x => x.ToEntity());

        await _blogLayoutRepository.CreateListAsync(models);
        await _blogLayoutRepository.SaveChangesAsync();

        return ApiResponse.Created(models.Select(x => x.Id));
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _blogLayoutRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new BlogPostLayoutDetailDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                CreatedDate = x.CreatedDate,
            })
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _blogLayoutRepository.GetByIdAsync(id);
        if (data == null)
            return ApiResponse.NotFound();

        var dto = data.ToDto();

        return ApiResponse.Success(dto);
    }

    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = _blogLayoutRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new BlogPostLayoutListDto
            {
                CreatedDate = x.CreatedDate,
                Description = x.Description,
                Id = x.Id,
                Name = x.Name,
            });

        var totalRecord = await data.CountAsync();
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            data = data
                .Where(x => x.Name.ToLower().Contains(query.Keyword.ToLower()) ||
                x.Description != null && x.Description.ToLower().Contains(query.Keyword.ToLower())
            );

        }

        var pagedData = new PagingData<BlogPostLayoutListDto>
        {
            CurrentPage = query.PageIndex,
            PageSize = query.PageSize,
            DataSource = await data.Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize).ToListAsync(),
            Total = totalRecord,
            TotalFiltered = await data.CountAsync()
        };

        return ApiResponse.Success(pagedData);
    }

    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _blogLayoutRepository.GetPagedAsync(parameters);

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _blogLayoutRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _blogLayoutRepository.SaveChangesAsync();
        return ApiResponse.Success();
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateAsync(UpdateBlogPostLayoutDto obj)
    {
        var isExistName = await _blogLayoutRepository.AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == obj.Name.ToLower() && x.Id != obj.Id);
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        var existData = await _blogLayoutRepository.GetByIdAsync(obj.Id);
        if (existData == null)
            return ApiResponse.NotFound();

        obj.ToEntity(existData);

        await _blogLayoutRepository.UpdateAsync(existData);
        await _blogLayoutRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateBlogPostLayoutDto> obj)
    {
        var listIds = obj.Select(x => x.Id).ToList();

        var existData = await _blogLayoutRepository
            .FindByConditionAsync(x => !x.IsDeleted && listIds.Contains(x.Id));

        if (listIds.Count != existData.Count)
            return ApiResponse.BadRequest();

        foreach (var item in obj)
        {
            var existObj = existData.Find(x => x.Id == item.Id);
            if (existObj != null)
                item.ToEntity(existObj);
        }

        await _blogLayoutRepository.UpdateListAsync(existData);
        await _blogLayoutRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }
}
