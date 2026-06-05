using System;
using Backend.Application.DTOs.NotificationTypes;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface INotificationTypeService : IServiceBase<int, CreateNotificationTypeDto, UpdateNotificationTypeDto, DTParameter>
{
}
