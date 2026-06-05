using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.BlogPostCategories;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class BlogCategoryService : IBlogCategoryService
{
    private readonly IBlogCategoryRepository _blogCategoryRepository;

    public BlogCategoryService(IBlogCategoryRepository blogCategoryRepository)
    {
        _blogCategoryRepository = blogCategoryRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateBlogPostCategoryDto obj)
    {
        var isExistName = await _blogCategoryRepository.AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == obj.Name.ToLower());
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        var isExistColor = await _blogCategoryRepository.AnyAsync(x => !x.IsDeleted && x.Color == obj.Color);
        if (isExistColor)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Color),
                ApiCodeConstants.Common.DuplicatedData
            );
        var model = obj.ToEntity();

        await _blogCategoryRepository.CreateAsync(model);
        await _blogCategoryRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id);
    }

    public async Task<ApiResponse> CreateListAsync(IEnumerable<CreateBlogPostCategoryDto> objs)
    {
        var models = objs.Select(x => x.ToEntity());

        await _blogCategoryRepository.CreateListAsync(models);
        await _blogCategoryRepository.SaveChangesAsync();

        return ApiResponse.Created(models.Select(x => x.Id));
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _blogCategoryRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new BlogPostCategoryDetailDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Color = x.Color,
                SeoAlias = x.SeoAlias,
                CreatedDate = x.CreatedDate,
            })
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _blogCategoryRepository.GetByIdAsync(id);
        if (data == null)
            return ApiResponse.NotFound();

        var dto = data.ToDto();

        return ApiResponse.Success(dto);
    }

    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = _blogCategoryRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new BlogPostCategoryListDto
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

        var pagedData = new PagingData<BlogPostCategoryListDto>
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
        var data = await _blogCategoryRepository.GetPagedAsync(parameters);

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _blogCategoryRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _blogCategoryRepository.SaveChangesAsync();
        return ApiResponse.Success();
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateAsync(UpdateBlogPostCategoryDto obj)
    {
        var isExistName = await _blogCategoryRepository.AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == obj.Name.ToLower() && x.Id != obj.Id);
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        var isExistColor = await _blogCategoryRepository.AnyAsync(x => !x.IsDeleted && x.Color == obj.Color && x.Id != obj.Id);
        if (isExistColor)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Color),
                ApiCodeConstants.Common.DuplicatedData
            );
        var existData = await _blogCategoryRepository.GetByIdAsync(obj.Id);
        if (existData == null)
            return ApiResponse.NotFound();

        obj.ToEntity(existData);

        await _blogCategoryRepository.UpdateAsync(existData);
        await _blogCategoryRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateBlogPostCategoryDto> obj)
    {
        var listIds = obj.Select(x => x.Id).ToList();

        var existData = await _blogCategoryRepository
            .FindByConditionAsync(x => !x.IsDeleted && listIds.Contains(x.Id));

        if (listIds.Count != existData.Count)
            return ApiResponse.BadRequest();

        foreach (var item in obj)
        {
            var existObj = existData.Find(x => x.Id == item.Id);
            if (existObj != null)
                item.ToEntity(existObj);
        }

        await _blogCategoryRepository.UpdateListAsync(existData);
        await _blogCategoryRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }
}
