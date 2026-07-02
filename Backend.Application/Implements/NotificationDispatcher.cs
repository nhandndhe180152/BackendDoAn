using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Application.Implements;

/// <summary>
/// Tạo thông báo (DB) + push FCM theo mã sự kiện. Tự bảo đảm danh mục tồn tại,
/// gộp người nhận theo userId và theo vai trò, và bỏ qua lỗi gửi để không ảnh
/// hưởng nghiệp vụ.
/// </summary>
public class NotificationDispatcher : INotificationDispatcher
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserNotificationRepository _userNotificationRepository;
    private readonly INotificationCategoryRepository _notificationCategoryRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IUserDeviceRepository _userDeviceRepository;
    private readonly IFireBaseService _fireBaseService;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationRepository notificationRepository,
        IUserNotificationRepository userNotificationRepository,
        INotificationCategoryRepository notificationCategoryRepository,
        IUserRoleRepository userRoleRepository,
        IUserDeviceRepository userDeviceRepository,
        IFireBaseService fireBaseService,
        ILoggerFactory loggerFactory)
    {
        _notificationRepository = notificationRepository;
        _userNotificationRepository = userNotificationRepository;
        _notificationCategoryRepository = notificationCategoryRepository;
        _userRoleRepository = userRoleRepository;
        _userDeviceRepository = userDeviceRepository;
        _fireBaseService = fireBaseService;
        _logger = loggerFactory.CreateLogger<NotificationDispatcher>();
    }

    public async Task DispatchAsync(
        string code,
        NotificationTarget target,
        object[]? args = null,
        string? directionId = null,
        int? createdBy = null)
    {
        try
        {
            if (target == null || !NotificationConstants.Catalog.TryGetValue(code, out var tpl))
            {
                _logger.LogWarning("[Notify] Không tìm thấy mã thông báo: {Code}", code);
                return;
            }

            // Gộp người nhận: userId trực tiếp + user thuộc các vai trò.
            var userIds = new HashSet<int>(target.UserIds.Where(x => x > 0));
            if (target.RoleIds != null && target.RoleIds.Count > 0)
            {
                var roleUserIds = await _userRoleRepository
                    .FindByCondition(x => target.RoleIds.Contains(x.RoleId) && !x.IsDeleted)
                    .Select(x => x.UserId)
                    .ToListAsync();
                foreach (var uid in roleUserIds) userIds.Add(uid);
            }
            if (userIds.Count == 0) return;

            var title = (args != null && args.Length > 0) ? string.Format(tpl.Title, args) : tpl.Title;
            var content = (args != null && args.Length > 0) ? string.Format(tpl.Content, args) : tpl.Content;

            var categoryId = await EnsureCategoryAsync(tpl.Category, tpl.Color);

            // 1) Lưu Notification + UserNotification vào DB.
            var notification = new Notification
            {
                NotificationCategoryId = categoryId,
                Title = title,
                Content = content,
                DirectionId = directionId,
                CreatedBy = createdBy,
                CreatedDate = DateTime.Now,
            };
            await _notificationRepository.CreateAsync(notification);
            await _notificationRepository.SaveChangesAsync();

            var userNotifications = userIds.Select(uid => new UserNotification
            {
                NotificationId = notification.Id,
                UserId = uid,
                IsRead = false,
                CreatedBy = createdBy,
                CreatedDate = DateTime.Now,
            }).ToList();
            await _userNotificationRepository.CreateListAsync(userNotifications);
            await _userNotificationRepository.SaveChangesAsync();

            // 2) Push FCM tới các thiết bị của người nhận.
            var tokens = await _userDeviceRepository
                .FindByCondition(x =>
                    userIds.Contains(x.UserId) &&
                    !x.IsDeleted &&
                    x.DeviceToken != null &&
                    x.DeviceToken != "")
                .Select(x => x.DeviceToken!)
                .Distinct()
                .ToListAsync();

            if (tokens.Count > 0)
            {
                await _fireBaseService.SendNotificationAsync(
                    tokens, title, content, categoryId.ToString(), directionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Notify] Lỗi gửi thông báo {Code}: {Message}", code, ex.Message);
        }
    }

    /// <summary>Lấy danh mục theo tên, tạo mới nếu chưa có.</summary>
    private async Task<int> EnsureCategoryAsync(string name, string color)
    {
        var existing = await _notificationCategoryRepository
            .FirstOrDefaultAsync(x => x.Name == name && !x.IsDeleted);
        if (existing != null) return existing.Id;

        var category = new NotificationCategory
        {
            Name = name,
            Color = color,
            Description = name,
            CreatedDate = DateTime.Now,
        };
        await _notificationCategoryRepository.CreateAsync(category);
        await _notificationCategoryRepository.SaveChangesAsync();
        return category.Id;
    }
}
