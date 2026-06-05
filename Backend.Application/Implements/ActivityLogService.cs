using System;
using Backend.Application.DependencyInjection.Extentions;
using Backend.Application.DTOs.ActivityLogs;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.DTParameters;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class ActivityLogService : IActivityLogService
{
    private readonly IActivityLogRepository _activityLogRepository;
    private readonly IUserRepository _userRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ActivityLogService(IActivityLogRepository activityLogRepository, IHttpContextAccessor httpContextAccessor, IUserRepository userRepository)
    {
        _activityLogRepository = activityLogRepository;
        _httpContextAccessor = httpContextAccessor;
        _userRepository = userRepository;
    }


    public async Task<ApiResponse> CreateAsync(CreateActivityLogDto obj)
    {
        var model = obj.ToEntity();
        model.UserAgent = _httpContextAccessor?.HttpContext?.Request.Headers.UserAgent;
        model.IpAddress = _httpContextAccessor?.HttpContext?.GetRemoteHostIpAddress();

        await _activityLogRepository.CreateAsync(model);
        await _activityLogRepository.SaveChangesAsync();

        return ApiResponse.Success(model.Id);
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateActivityLogDto> objs)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetAllAsync()
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await (from a in _activityLogRepository.GetAll()
                          join b in _userRepository.GetAll() on a.CreatedBy equals b.Id into groupAB
                          from c in groupAB.DefaultIfEmpty()
                          where !a.IsDeleted && !c.IsDeleted && a.Id == id
                          select new ActivityLogDetailDto
                          {
                              Id = a.Id,
                              Action = a.Action,
                              Description = a.Description,
                              IpAddress = a.IpAddress,
                              UserAgent = a.UserAgent,
                              CreatedDate = a.CreatedDate,
                              CreatedUser = c == null ? null : new DataItem<int>
                              {
                                  Id = c.Id,
                                  Name = c.FirstName + c.LastName
                              }
                          })
                  .FirstOrDefaultAsync();

        return ApiResponse.Success(data);
    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetPagedAsync(ActivityLogDTParameters parameters)
    {
        var data = await _activityLogRepository.GetPagedAsync(parameters);

        return ApiResponse.Success(data);
    }

    public Task<ApiResponse> SoftDeleteAsync(int id)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> UpdateAsync(UpdateActivityLogDto obj)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateActivityLogDto> obj)
    {
        throw new NotImplementedException();
    }
}
