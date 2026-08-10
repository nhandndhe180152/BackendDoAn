using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.Interfaces;
using Backend.Application.BackgroundJobs.FcmNotificationRetry;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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
    private readonly IApplicationDbContext _context;
    private readonly IFcmClient _fcmClient;
    private readonly IFcmFailureClassifier _failureClassifier;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationRepository notificationRepository,
        IUserNotificationRepository userNotificationRepository,
        INotificationCategoryRepository notificationCategoryRepository,
        INotificationTypeRepository notificationTypeRepository,
        IUserRoleRepository userRoleRepository,
        IUserDeviceRepository userDeviceRepository,
        IApplicationDbContext context,
        IFcmClient fcmClient,
        IFcmFailureClassifier failureClassifier,
        ILoggerFactory loggerFactory)
    {
        _notificationRepository = notificationRepository;
        _userNotificationRepository = userNotificationRepository;
        _notificationCategoryRepository = notificationCategoryRepository;
        _notificationTypeRepository = notificationTypeRepository;
        _userRoleRepository = userRoleRepository;
        _userDeviceRepository = userDeviceRepository;
        _context = context;
        _fcmClient = fcmClient;
        _failureClassifier = failureClassifier;
        _logger = loggerFactory.CreateLogger<NotificationDispatcher>();
    }

    public async Task DispatchAsync(
        string code,
        NotificationTarget target,
        object[]? args = null,
        string? directionId = null,
        int? createdBy = null,
        string? referenceType = null,
        int? referenceId = null)
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

            // Với thông báo HÀNH ĐỘNG (có người thực hiện), bổ sung dòng "Người thực hiện: Tên (Vai trò)".
            // Cảnh báo nền (job) có createdBy = null nên KHÔNG kèm dòng này.
            var actorSuffix = await BuildActorSuffixAsync(createdBy);
            if (!string.IsNullOrEmpty(actorSuffix))
            {
                content += actorSuffix;
            }

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


            // 2) Push FCM tới các thiết bị của người nhận và ghi nhận log.
            var devices = await _userDeviceRepository
                .FindByCondition(x =>
                    userIds.Contains(x.UserId) &&
                    !x.IsDeleted &&
                    x.DeviceToken != null &&
                    x.DeviceToken != "")
                .ToListAsync();

            if (devices.Count > 0)
            {
                var uniqueDevices = devices.GroupBy(d => d.DeviceToken).Select(g => g.First()).ToList();
                var tokens = uniqueDevices.Select(d => d.DeviceToken!).ToList();

                List<FcmSendOutcome>? outcomes = null;
                Exception? globalException = null;

                try
                {
                    outcomes = await _fcmClient.SendMulticastAsync(tokens, title, content, new Dictionary<string, string>
                    {
                        { "categoryId", categoryId.Value.ToString() },
                        { "directionId", directionId ?? "" }
                    }, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FCM] Primary send failed globally (possible credentials/project config issue): {Message}", ex.Message);
                    globalException = ex;
                }

                var logs = new List<FcmNotificationLog>();
                var now = DateTime.UtcNow;

                for (int i = 0; i < uniqueDevices.Count; i++)
                {
                    var device = uniqueDevices[i];
                    var success = false;
                    string? providerMsgId = null;
                    string? errorMsg = null;
                    string? errCode = null;

                    var outcome = outcomes?.FirstOrDefault(o => o.Token == device.DeviceToken);

                    if (outcome != null)
                    {
                        success = outcome.IsSuccess;
                        providerMsgId = outcome.IsSuccess ? outcome.MessageId : null;
                        errorMsg = outcome.IsSuccess ? null : outcome.Exception?.Message;

                        if (outcome.Exception != null)
                        {
                            errCode = _failureClassifier.GetErrorCode(outcome.Exception) ?? "Exception";
                        }

                        if (!outcome.IsSuccess)
                        {
                            _logger.LogError("[FCM] Primary send failed for device {DeviceId} (Token: {Token}): {Error}", device.Id, device.DeviceToken, errorMsg);
                            if (errCode == "Unregistered" || errCode == "SenderIdMismatch")
                            {
                                device.IsDeleted = true;
                                await _userDeviceRepository.UpdateAsync(device);
                            }
                        }
                    }
                    else if (globalException != null)
                    {
                        errorMsg = globalException.Message;
                        errCode = "GLOBAL_SEND_ERROR";
                    }

                    var log = new FcmNotificationLog
                    {
                        UserId = device.UserId,
                        UserDeviceId = device.Id,
                        Title = title,
                        Body = content,
                        DataPayload = JsonConvert.SerializeObject(new Dictionary<string, string>
                        {
                            { "categoryId", categoryId.Value.ToString() },
                            { "directionId", directionId ?? "" }
                        }),
                        TriggerType = code,
                        ReferenceType = referenceType,
                        ReferenceId = referenceId,
                        IsSent = success,
                        SentAt = success ? now : null,
                        ErrorMessage = errorMsg,
                        LastErrorCode = errCode,
                        ProviderMessageId = providerMsgId,
                        Status = success ? FcmNotificationRetryConstants.Status.Sent : FcmNotificationRetryConstants.Status.Pending,
                        AttemptCount = 1,
                        LastAttemptAt = now,
                        CreatedDate = now,
                        IsDeleted = false
                    };

                    logs.Add(log);
                }

                await _context.FcmNotificationLogs.AddRangeAsync(logs);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Notify] Lỗi gửi thông báo {Code}: {Message}", code, ex.Message);
        }
    }

    /// <summary>
    /// Dựng dòng "Người thực hiện: {Họ tên} ({Vai trò})" cho thông báo hành động.
    /// Trả về chuỗi rỗng nếu không có người thực hiện (thông báo/cảnh báo do hệ thống phát)
    /// hoặc không tìm thấy người dùng — khi đó nội dung giữ nguyên như cũ.
    /// </summary>
    private async Task<string> BuildActorSuffixAsync(int? createdBy)
    {
        if (createdBy == null || createdBy <= 0) return string.Empty;

        var actor = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == createdBy && !u.IsDeleted)
            .Select(u => new
            {
                u.FirstName,
                u.LastName,
                RoleNames = _context.UserRoles
                    .Where(ur => ur.UserId == u.Id && !ur.IsDeleted)
                    .Join(_context.Roles.Where(r => !r.IsDeleted),
                          ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (actor == null) return string.Empty;

        var fullName = $"{actor.LastName} {actor.FirstName}".Trim();
        if (string.IsNullOrWhiteSpace(fullName)) return string.Empty;

        var roleText = actor.RoleNames != null && actor.RoleNames.Count > 0
            ? $" ({string.Join(", ", actor.RoleNames)})"
            : string.Empty;

        return $"\nNgười thực hiện: {fullName}{roleText}.";
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
