using System;
using Backend.Application.DTOs.UserNotifications;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IUserNotificationService : IServiceBase<int, CreateUserNotificationDto, UpdateUserNotificationDto, DTParameter>
{
}
