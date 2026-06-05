using System;
using Backend.Application.DTOs.NotificationCategories;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface INotificationCategoryService : IServiceBase<int, CreateNotificationCategoryDto, UpdateNotificationCategoryDto, DTParameter>
{
}
