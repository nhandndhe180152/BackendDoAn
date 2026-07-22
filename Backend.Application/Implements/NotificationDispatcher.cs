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
    private readonly INotificationTypeRepository _notificationTypeRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IUserDeviceRepository _userDeviceRepository;
    private readonly IFireBaseService _fireBaseService;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationRepository notificationRepository,
        IUserNotificationRepository userNotificationRepository,
        INotificationCategoryRepository notificationCategoryRepository,
        INotificationTypeRepository notificationTypeRepository,
        IUserRoleRepository userRoleRepository,
        IUserDeviceRepository userDeviceRepository,
        IFireBaseService fireBaseService,
        ILoggerFactory loggerFactory)
    {
        _notificationRepository = notificationRepository;
        _userNotificationRepository = userNotificationRepository;
        _notificationCategoryRepository = notificationCategoryRepository;
        _notificationTypeRepository = notificationTypeRepository;
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

            // LOẠI + DANH MỤC lấy từ dữ liệu đã lưu trong DB (phân giải theo TÊN, không hard-code Id).
            var typeId = await ResolveTypeIdByNameAsync(NotificationConstants.TypeName.System);
            if (typeId == null)
            {
                _logger.LogWarning(
                    "[Notify] Chưa có LOẠI thông báo tên '{TypeName}' trong DB. Bỏ qua thông báo {Code}. " +
                    "Hãy thêm loại này qua trang quản trị.",
                    NotificationConstants.TypeName.System, code);
                return;
            }

            var categoryId = await ResolveCategoryIdByNameAsync(tpl.Category);
            if (categoryId == null)
            {
                _logger.LogWarning(
                    "[Notify] Chưa có DANH MỤC thông báo tên '{CategoryName}' trong DB. Bỏ qua thông báo {Code}. " +
                    "Hãy thêm danh mục này qua trang quản trị.",
                    tpl.Category, code);
                return;
            }

            // 1) Lưu Notification + UserNotification vào DB.
            var notification = new Notification
            {
                NotificationCategoryId = categoryId.Value,
                NotificationTypeId = typeId.Value, // FK bắt buộc — luôn là loại "Hệ thống" lấy từ DB
                Title = title,
                Content = content,
                DirectionId = directionId,
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
            };
            await _notificationRepository.CreateAsync(notification);
            await _notificationRepository.SaveChangesAsync();

            var userNotifications = userIds.Select(uid => new UserNotification
            {
                NotificationId = notification.Id,
                UserId = uid,
                IsRead = false,
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
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
                    tokens, title, content, categoryId.Value.ToString(), directionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Notify] Lỗi gửi thông báo {Code}: {Message}", code, ex.Message);
        }
    }

    /// <summary>
    /// Phân giải Id LOẠI thông báo theo TÊN từ DB (không tạo mới).
    /// Trả về null nếu chưa có — admin cần thêm dữ liệu qua trang quản trị.
    /// </summary>
    private async Task<int?> ResolveTypeIdByNameAsync(string name)
    {
        var existing = await _notificationTypeRepository
            .FirstOrDefaultAsync(x => x.Name == name && !x.IsDeleted);
        return existing?.Id;
    }

    /// <summary>
    /// Phân giải Id DANH MỤC thông báo theo TÊN từ DB (không tạo mới).
    /// Trả về null nếu chưa có — admin cần thêm dữ liệu qua trang quản trị.
    /// </summary>
    private async Task<int?> ResolveCategoryIdByNameAsync(string name)
    {
        var existing = await _notificationCategoryRepository
            .FirstOrDefaultAsync(x => x.Name == name && !x.IsDeleted);
        return existing?.Id;
    }
}
