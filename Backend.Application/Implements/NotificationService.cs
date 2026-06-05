using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.Notifications;
using Backend.Application.DTOs.UserNotifications;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Application.Implements;

public class NotificationService : INotificationService
{
    private readonly IUserNotificationRepository _userNotificationRepository;
    private readonly IUserDeviceRepository _userDeviceRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationCategoryRepository _notificationCategoryRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<NotificationService> _logger;
    private readonly IFireBaseService _fireBaseService;

    public NotificationService(INotificationRepository notificationRepository, IUserNotificationRepository userNotificationRepository, INotificationCategoryRepository notificationCategoryRepository, ILoggerFactory loggerFactory, IHttpContextAccessor httpContextAccessor, IUserDeviceRepository userDeviceRepository, IFireBaseService fireBaseService)
    {
        _notificationRepository = notificationRepository;
        _userNotificationRepository = userNotificationRepository;
        _notificationCategoryRepository = notificationCategoryRepository;
        _logger = loggerFactory.CreateLogger<NotificationService>();
        _httpContextAccessor = httpContextAccessor;
        _userDeviceRepository = userDeviceRepository;
        _fireBaseService = fireBaseService;
    }

    public async Task<ApiResponse> CreateAsync(CreateNotificationDto obj)
    {
        var model = obj.ToEntity();
        try
        {
            await _notificationRepository.BeginTransactionAsync();
            await _notificationRepository.CreateAsync(model);
            await _notificationRepository.SaveChangesAsync();

            if (obj.UserIds.Any())
            {
                var objs = obj.UserIds.Select(x => new UserNotification
                {
                    NotificationId = model.Id,
                    UserId = x,
                    IsRead = false,
                    CreatedBy = obj.CreatedBy,
                    CreatedDate = DateTime.Now,
                });
                await _userNotificationRepository.CreateListAsync(objs);
                await _userNotificationRepository.SaveChangesAsync();
            }
            await _notificationRepository.EndTransactionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create notification with message {Message}", ex.Message);
            await _notificationRepository.RollbackTransactionAsync();

            return ApiResponse.InternalServerError();
        }
        return ApiResponse.Created(model.Id);
    }

    public async Task<ApiResponse> CreateListAsync(IEnumerable<CreateNotificationDto> objs)
    {
        var model = objs.Select(x => x.ToEntity());

        await _notificationRepository.CreateListAsync(model);
        await _notificationRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Select(x => x.Id));
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _notificationRepository
           .FindByCondition(x => !x.IsDeleted)
           .Select(x => new NotificationListDto
           {
               Id = x.Id,
               CreatedDate = x.CreatedDate,
               NotificationCategoryId = x.NotificationCategoryId,
               NotificationCategoryName = x.NotificationCategory.Name,
               DirectionId = x.DirectionId,
               Title = x.Title,
               Content = x.Content,
           })
           .ToListAsync();
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _notificationRepository
            .FindByCondition(x => !x.IsDeleted && x.Id == id)
            .Select(x => new NotificationDetailDto()
            {
                Id = x.Id,
                CreatedDate = x.CreatedDate,
                NotificationCategoryId = x.NotificationCategoryId,
                DirectionId = x.DirectionId,
                Title = x.Title,
                Content = x.Content,
                NotificationCategory = new DataItem<int>
                {
                    Id = x.NotificationCategoryId,
                    Name = x.NotificationCategory.Name
                },
                NotificationUsers = x.UserNotifications
                    .Where(xx => !xx.IsDeleted)
                    .Select(xx => new DataItem<int>
                    {
                        Id = xx.UserId,
                        Name = xx.User.FirstName + " " + xx.User.LastName,
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (data == null) return ApiResponse.NotFound();
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = _notificationRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new NotificationListDto()
            {
                Id = x.Id,
                CreatedDate = x.CreatedDate,
                NotificationCategoryId = x.NotificationCategoryId,
                NotificationCategoryName = x.NotificationCategory.Name,
                DirectionId = x.DirectionId,
                Title = x.Title,
                Content = x.Content,
            });

        var totalRecord = await data.CountAsync();
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            data = data
                .Where(x => x.Title.ToLower().Contains(query.Keyword.ToLower()) ||
                x.Content.ToLower().Contains(query.Keyword.ToLower()) ||
                x.NotificationCategoryName.ToLower().Contains(query.Keyword.ToLower()) ||
                x.DirectionId != null && x.DirectionId.ToLower().Contains(query.Keyword.ToLower())
            );

        }

        if (!string.IsNullOrEmpty(query.OrderBy))
        {
            data = data
                .OrderByDynamic(query.OrderBy, query.SortType == "asc" ? LinqExtensions.Order.Asc : LinqExtensions.Order.Desc);
        }

        var pagedData = new PagingData<NotificationListDto>
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

    public async Task<ApiResponse> GetPagedAsync(NotificationDTParameters parameters)
    {
        var currentRoleIds = _httpContextAccessor.HttpContext?.GetCurrentRoleIds();
        var currentUserId = _httpContextAccessor.HttpContext?.GetCurrentUserId();

        var isAdmin = currentRoleIds != null && currentRoleIds.Any(x => x == CommonConstants.Role.ADMIN);
        parameters.IsAdmin = isAdmin;
        parameters.UserId = currentUserId ?? 0;

        var data = await _notificationRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var existData = await _notificationRepository
        .FindByCondition(x => !x.IsDeleted && x.Id == id)
        .FirstOrDefaultAsync();
        if (existData == null)
            return ApiResponse.NotFound();
        try
        {
            await _notificationRepository.BeginTransactionAsync();

            var isDeleted = await _notificationRepository.SoftDeleteAsync(id);
            if (!isDeleted)
                return ApiResponse.BadRequest();

            var userNotificationObjs = await _userNotificationRepository
                .FindByCondition(x => x.NotificationId == id)
                .Select(x => x.Id)
                .ToListAsync();

            await _userNotificationRepository.SoftDeleteListAsync(userNotificationObjs);
            await _notificationRepository.SaveChangesAsync();
            await _notificationRepository.EndTransactionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete notification with message {Message}", ex.Message);
            await _notificationRepository.RollbackTransactionAsync();
            return ApiResponse.InternalServerError();
        }
        return ApiResponse.Success(true);
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateAsync(UpdateNotificationDto obj)
    {
        var existData = await _notificationRepository
            .FindByCondition(x => !x.IsDeleted && x.Id == obj.Id)
            .FirstOrDefaultAsync();
        if (existData == null)
            return ApiResponse.NotFound();

        obj.ToEntity(existData);

        try
        {
            await _notificationRepository.BeginTransactionAsync();

            await _notificationRepository.UpdateAsync(existData);

            var oldObjs = await _userNotificationRepository
                .FindByCondition(x => x.NotificationId == obj.Id)
                .Select(x => x.Id)
                .ToListAsync();
            await _userNotificationRepository.SoftDeleteListAsync(oldObjs);

            var objs = obj.UserIds.Select(x => new UserNotification
            {
                UserId = x,
                NotificationId = obj.Id,
                CreatedBy = obj.UpdatedBy,
                CreatedDate = DateTime.Now
            });
            await _userNotificationRepository.CreateListAsync(objs);

            await _notificationRepository.SaveChangesAsync();
            await _notificationRepository.EndTransactionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update notification with message {Message}", ex.Message);
            await _notificationRepository.RollbackTransactionAsync();

            return ApiResponse.InternalServerError();
        }

        return ApiResponse.Success();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateNotificationDto> obj)
    {
        throw new NotImplementedException();
    }
    public async Task<ApiResponse> GetPagedByUserAsync(int userId, UserNotificationsSearchQuery query)
    {
        var data = from un in _userNotificationRepository.GetAll()
                   join n in _notificationRepository.GetAll() on un.NotificationId equals n.Id
                   join nc in _notificationCategoryRepository.GetAll() on n.NotificationCategoryId equals nc.Id
                   where un.UserId == userId
                         && !n.IsDeleted
                         && !nc.IsDeleted
                         && (query.IsRead == null || un.IsRead == query.IsRead)
                   select new UserNotificationDto
                   {
                       Id = un.Id,
                       NotificationId = un.NotificationId,
                       Title = n.Title,
                       Content = n.Content,
                       DirectionId = n.DirectionId,
                       NotificationCategoryId = n.NotificationCategoryId,
                       NotificationCategoryName = nc.Name,
                       IsRead = un.IsRead,
                       CreatedDate = n.CreatedDate
                   };

        var totalRecord = await data.CountAsync();

        if (!string.IsNullOrEmpty(query.OrderBy))
        {
            data = data.OrderByDynamic(
                query.OrderBy,
                query.SortType == "asc" ? LinqExtensions.Order.Asc : LinqExtensions.Order.Desc
            );
        }
        else
        {
            data = data.OrderByDynamic("CreatedDate", LinqExtensions.Order.Desc);
        }

        var pagedData = new PagingData<UserNotificationDto>
        {
            CurrentPage = query.PageIndex,
            PageSize = query.PageSize,
            DataSource = await data
                .Skip((query.PageIndex - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(),
            Total = totalRecord,
            TotalFiltered = await data.CountAsync()
        };

        return ApiResponse.Success(pagedData);
    }

    public async Task<ApiResponse> UpdateStatusAsync(int userNoficationId, bool isRead)
    {
        var currentUserId = _httpContextAccessor.HttpContext?.GetCurrentUserId();
        var existData = await _userNotificationRepository
            .FindByCondition(x => x.Id == userNoficationId)
            .FirstOrDefaultAsync();
        if (existData == null || existData.UserId != currentUserId)
            return ApiResponse.BadRequest();

        existData.IsRead = isRead;
        existData.LastModifiedDate = DateTime.Now;
        existData.UpdatedBy = _httpContextAccessor.HttpContext?.GetCurrentUserId();

        await _userNotificationRepository.UpdateAsync(existData);
        await _userNotificationRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> SoftDeleteUserNotificationAsync(int userNoficationId)
    {
        var currentUserId = _httpContextAccessor.HttpContext?.GetCurrentUserId();
        var existData = await _userNotificationRepository
            .FindByCondition(x => x.Id == userNoficationId)
            .FirstOrDefaultAsync();
        if (existData == null || existData.UserId != currentUserId)
            return ApiResponse.BadRequest();

        existData.IsDeleted = true;
        existData.LastModifiedDate = DateTime.Now;
        existData.UpdatedBy = _httpContextAccessor.HttpContext?.GetCurrentUserId();

        await _userNotificationRepository.UpdateAsync(existData);
        await _userNotificationRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> TestFireBase(int userId)
    {
        try
        {
            var listUserDevice = await _userDeviceRepository
                .FindByCondition(x => x.UserId == userId && !x.IsDeleted && !String.IsNullOrEmpty(x.DeviceToken))
                .Select(x => x.DeviceToken)
                .ToListAsync();
            if (listUserDevice.Any())
            {
                _fireBaseService.SendNotificationAsync(listUserDevice, "Test", "Test nè", "1001", "1002");
            }
            return ApiResponse.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send fire base notification with message {Message}", ex.Message);
            return ApiResponse.InternalServerError();
        }

    }
}
