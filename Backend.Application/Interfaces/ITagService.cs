using System;
using Backend.Application.DTOs.Tags;
using Backend.Domain.DTParameters;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface ITagService : IServiceBase<int, CreateTagDto, UpdateTagDto, TagDTParamters>
{
    Task<ApiResponse> GetPagedAsync(TagSearchQuery query);
}
