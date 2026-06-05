using System;
using Backend.Application.DTOs.UserNotifications;
using Backend.Application.Interfaces;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;

namespace Backend.Application.Implements;

public class UserNotificationService : IUserNotificationService
{
    private readonly IUserNotificationRepository _userNotificationRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationCategoryRepository _notificationCategoryRepository;

    public UserNotificationService(IUserNotificationRepository userNotificationRepository, INotificationRepository notificationRepository, INotificationCategoryRepository notificationCategoryRepository)
    {
        _userNotificationRepository = userNotificationRepository;
        _notificationRepository = notificationRepository;
        _notificationCategoryRepository = notificationCategoryRepository;
    }

    public Task<ApiResponse> CreateAsync(CreateUserNotificationDto obj)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateUserNotificationDto> objs)
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

    public Task<ApiResponse> UpdateAsync(UpdateUserNotificationDto obj)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateUserNotificationDto> obj)
    {
        throw new NotImplementedException();
    }
}
