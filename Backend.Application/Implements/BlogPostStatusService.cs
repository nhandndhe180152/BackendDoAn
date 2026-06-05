using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.BlogPostStatuses;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class BlogPostStatusService : IBlogPostStatusService
{
    private readonly IBlogPostStatusRepository _blogPostStatusRepository;

    public BlogPostStatusService(IBlogPostStatusRepository blogPostStatusRepository)
    {
        _blogPostStatusRepository = blogPostStatusRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateBlogPostStatusDto obj)
    {
        var isExistName = await _blogPostStatusRepository.AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == obj.Name.ToLower());
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        var isExistColor = await _blogPostStatusRepository.AnyAsync(x => !x.IsDeleted && x.Color == obj.Color);
        if (isExistColor)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Color),
                ApiCodeConstants.Common.DuplicatedData
            );
        var model = obj.ToEntity();

        await _blogPostStatusRepository.CreateAsync(model);
        await _blogPostStatusRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id);
    }

    public async Task<ApiResponse> CreateListAsync(IEnumerable<CreateBlogPostStatusDto> objs)
    {
        var models = objs.Select(x => x.ToEntity());

        await _blogPostStatusRepository.CreateListAsync(models);
        await _blogPostStatusRepository.SaveChangesAsync();

        return ApiResponse.Created(models.Select(x => x.Id));
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _blogPostStatusRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new BlogPostStatusDetailDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Color = x.Color,
                CreatedDate = x.CreatedDate,
            })
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _blogPostStatusRepository.GetByIdAsync(id);
        if (data == null)
            return ApiResponse.NotFound();

        var dto = data.ToDto();

        return ApiResponse.Success(dto);
    }

    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = _blogPostStatusRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new BlogPostStatusListDto
            {
                Color = x.Color,
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

        var pagedData = new PagingData<BlogPostStatusListDto>
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
        var data = await _blogPostStatusRepository.GetPagedAsync(parameters);

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _blogPostStatusRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        return ApiResponse.Success();
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateAsync(UpdateBlogPostStatusDto obj)
    {
        var isExistName = await _blogPostStatusRepository.AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == obj.Name.ToLower() && x.Id != obj.Id);
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        var isExistColor = await _blogPostStatusRepository.AnyAsync(x => !x.IsDeleted && x.Color == obj.Color && x.Id != obj.Id);
        if (isExistColor)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Color),
                ApiCodeConstants.Common.DuplicatedData
            );
        var existData = await _blogPostStatusRepository.GetByIdAsync(obj.Id);
        if (existData == null)
            return ApiResponse.NotFound();

        obj.ToEntity(existData);

        await _blogPostStatusRepository.UpdateAsync(existData);
        await _blogPostStatusRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateBlogPostStatusDto> obj)
    {
        var listIds = obj.Select(x => x.Id).ToList();

        var existData = await _blogPostStatusRepository
            .FindByConditionAsync(x => !x.IsDeleted && listIds.Contains(x.Id));

        if (listIds.Count != existData.Count)
            return ApiResponse.BadRequest();

        foreach (var item in obj)
        {
            var existObj = existData.Find(x => x.Id == item.Id);
            if (existObj != null)
                item.ToEntity(existObj);
        }

        await _blogPostStatusRepository.UpdateListAsync(existData);
        await _blogPostStatusRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }
}
