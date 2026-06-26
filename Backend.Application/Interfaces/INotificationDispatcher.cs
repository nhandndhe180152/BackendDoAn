using System.Collections.Generic;
using System.Threading.Tasks;

namespace Backend.Application.Interfaces;

/// <summary>Đối tượng nhận thông báo: theo userId trực tiếp và/hoặc theo vai trò.</summary>
public class NotificationTarget
{
    public List<int> UserIds { get; set; } = new();
    public List<int> RoleIds { get; set; } = new();
}

/// <summary>
/// Phát thông báo theo mã sự kiện (NotificationConstants.Code): tạo Notification
/// + UserNotification trong DB và push FCM tới thiết bị của người nhận.
/// Lỗi gửi thông báo KHÔNG làm hỏng luồng nghiệp vụ chính.
/// </summary>
public interface INotificationDispatcher
{
    Task DispatchAsync(
        string code,
        NotificationTarget target,
        object[]? args = null,
        string? directionId = null,
        int? createdBy = null);
}
