using System.Threading.Tasks;

namespace Backend.Application.Interfaces;

/// <summary>
/// Phát tín hiệu realtime liên quan thiết bị qua SignalR (cài đặt ở tầng API).
/// </summary>
public interface IDevicePresenceNotifier
{
    /// <summary>Báo cho các phiên của user rằng danh sách/trạng thái thiết bị vừa đổi.</summary>
    Task NotifyDevicesChangedAsync(int userId);

    /// <summary>Buộc thiết bị (theo deviceId) đăng xuất tại chỗ.</summary>
    Task ForceLogoutDeviceAsync(string deviceId);
}
