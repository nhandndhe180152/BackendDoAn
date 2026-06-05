using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.AuditLogs;
using Backend.Application.Interfaces;
using Backend.Domain.DTParameters;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUserRepository _userRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    public AuditLogService(IAuditLogRepository auditLogRepository, IHttpContextAccessor httpContextAccessor, IUserRepository userRepository)
    {
        _auditLogRepository = auditLogRepository;
        _httpContextAccessor = httpContextAccessor;
        _userRepository = userRepository;
    }
    public Task<ApiResponse> CreateAsync(CreateAuditLogDto obj)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateAuditLogDto> objs)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetAllAsync()
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await (from a in _auditLogRepository.GetAll()
                          join b in _userRepository.GetAll() on a.CreatedBy equals b.Id into groupAB
                          from c in groupAB.DefaultIfEmpty()
                          where !a.IsDeleted && (c == null || !c.IsDeleted) && a.Id == id
                          select new AuditLogDetailDto
                          {
                              Id = a.Id,
                              Action = a.Action,
                              TargetType = a.TargetType,
                              TargetId = a.TargetId,
                              DataBefore = a.DataBefore,
                              DataAfter = a.DataAfter,
                              Description = a.Description,
                              IpAddress = a.IpAddress,
                              UserAgent = a.UserAgent,
                              CreatedDate = a.CreatedDate,
                              CreatedUser = c == null ? null : new DataItem<int>
                              {
                                  Id = c.Id,
                                  Name = c.FirstName + " " + c.LastName
                              }
                          })
              .FirstOrDefaultAsync();
        if (data == null)
            return ApiResponse.NotFound();

        data.TargetTypeName = CommonConstants.EntityDisplayMap.GetValueOrDefault(data.TargetType) ?? data.TargetType;
        data.ActionName = CommonConstants.ListAction.GetValueOrDefault(data.Action) ?? data.Action;

        return ApiResponse.Success(data);
    }

    public ApiResponse GetListAction()
    {
        var data = CommonConstants.ListAction
            .Select(x => new DataItem<string>
            {
                Id = x.Key,
                Name = x.Value,
            })
            .ToList();

        return ApiResponse.Success(data);
    }

    public ApiResponse GetListAuditEntity()
    {
        var data = CommonConstants.EntityDisplayMap
            .Select(x => new DataItem<string>
            {
                Id = x.Key,
                Name = x.Value,
            })
            .ToList();

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

    public async Task<ApiResponse> GetPagedAsync(AuditLogDTParameters parameters)
    {
        var currentRoleIds = _httpContextAccessor.HttpContext?.GetCurrentRoleIds();
        parameters.RoleIds = currentRoleIds ?? new List<int>();

        var data = await _auditLogRepository.GetPagedAsync(parameters);
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

    public Task<ApiResponse> UpdateAsync(UpdateAuditLogDto obj)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateAuditLogDto> objs)
    {
        throw new NotImplementedException();
    }
}
