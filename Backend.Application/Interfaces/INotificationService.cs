using System;
using Backend.Application.DTOs.Notifications;
using Backend.Application.DTOs.UserNotifications;
using Backend.Domain.DTParameters;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface INotificationService : IServiceBase<int, CreateNotificationDto, UpdateNotificationDto, NotificationDTParameters>
{
    public Task<ApiResponse> GetPagedByUserAsync(int userId, UserNotificationsSearchQuery query);
    Task<ApiResponse> UpdateStatusAsync(int userNoficationId, bool isRead);
    Task<ApiResponse> SoftDeleteUserNotificationAsync(int userNoficationId);
    Task<ApiResponse> TestFireBase(int userId);
}
