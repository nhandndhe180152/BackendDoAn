using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.Tags;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.DTParameters;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class TagService : ITagService
{
    private readonly ITagRepository _tagRepository;

    public TagService(ITagRepository tagRepository)
    {
        _tagRepository = tagRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateTagDto obj)
    {
        var model = obj.ToEntity();
        var isExistingTag = await _tagRepository.AnyAsync(
                x => x.Name.ToLower() == model.Name.ToLower() &&
                     x.TagTypeId == model.TagTypeId &&
                     !x.IsDeleted);

        if (isExistingTag)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        await _tagRepository.CreateAsync(model);
        await _tagRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id);
    }

    public async Task<ApiResponse> CreateListAsync(IEnumerable<CreateTagDto> objs)
    {
        var model = objs.Select(x => x.ToEntity());

        await _tagRepository.CreateListAsync(model);
        await _tagRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Select(x => x.Id));
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _tagRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new TagDetailDto
            {
                Id = x.Id,
                CreatedDate = x.CreatedDate,
                Description = x.Description,
                Name = x.Name,
                TagTypeId = x.TagTypeId,
                TagTypeName = x.TagType.Name,
            })
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _tagRepository.GetByIdAsync(id);
        if (data == null)
            return ApiResponse.NotFound();

        var dto = data.ToDto();

        return ApiResponse.Success(dto);
    }

    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = _tagRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new TagListDto
            {
                CreatedDate = x.CreatedDate,
                Description = x.Description,
                Id = x.Id,
                Name = x.Name,
                TagTypeId = x.TagTypeId,
                TagTypeName = x.TagType.Name,
            });

        var totalRecord = await data.CountAsync();
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            data = data
                .Where(x => x.Name.ToLower().Contains(query.Keyword.ToLower()) ||
                x.Description != null && x.Description.ToLower().Contains(query.Keyword.ToLower()) ||
                x.TagTypeName != null && x.TagTypeName.ToLower().Contains(query.Keyword.ToLower())
            );

        }

        if (!string.IsNullOrEmpty(query.OrderBy))
        {
            data = data
                .OrderByDynamic(query.OrderBy, query.SortType == "asc" ? LinqExtensions.Order.Asc : LinqExtensions.Order.Desc);
        }

        var pagedData = new PagingData<TagListDto>
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

    public async Task<ApiResponse> GetPagedAsync(TagDTParamters parameters)
    {
        var data = await _tagRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _tagRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _tagRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateAsync(UpdateTagDto obj)
    {
        var existData = await _tagRepository.GetByIdAsync(obj.Id);
        if (existData == null)
            return ApiResponse.NotFound();
        var isDuplicatedName = await _tagRepository.AnyAsync(
                x => x.Name.ToLower() == obj.Name.ToLower() &&
                     x.TagTypeId == obj.TagTypeId &&
                     x.Id != obj.Id &&
                     !x.IsDeleted);

        if (isDuplicatedName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        obj.ToEntity(existData);

        await _tagRepository.UpdateAsync(existData);
        await _tagRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateTagDto> obj)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetPagedAsync(TagSearchQuery query)
    {
        var data = _tagRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new TagListDto
            {
                CreatedDate = x.CreatedDate,
                Description = x.Description,
                Id = x.Id,
                Name = x.Name,
                TagTypeId = x.TagTypeId,
                TagTypeName = x.TagType.Name,
            });

        var totalRecord = await data.CountAsync();
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            data = data
                .Where(x => x.Name.ToLower().Contains(query.Keyword.ToLower()) ||
                x.Description != null && x.Description.ToLower().Contains(query.Keyword.ToLower()) ||
                x.TagTypeName != null && x.TagTypeName.ToLower().Contains(query.Keyword.ToLower())
            );

        }

        if (query.TagTypeId.HasValue)
            data = data
                .Where(x => x.TagTypeId == query.TagTypeId);

        if (!string.IsNullOrEmpty(query.OrderBy))
        {
            data = data
                .OrderByDynamic(query.OrderBy, query.SortType == "asc" ? LinqExtensions.Order.Asc : LinqExtensions.Order.Desc);
        }

        var pagedData = new PagingData<TagListDto>
        {
            CurrentPage = query.PageIndex,
            PageSize = query.PageSize,
            DataSource = await data.Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize).ToListAsync(),
            Total = totalRecord,
            TotalFiltered = await data.CountAsync()
        };

        return ApiResponse.Success(pagedData);
    }
}
