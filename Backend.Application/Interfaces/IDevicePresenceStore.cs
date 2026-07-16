using System;
using System.Collections.Generic;

namespace Backend.Application.Interfaces;

/// <summary>
/// Kho lưu trạng thái hiện diện của thiết bị theo kết nối SignalR (in-memory, 1 instance).
/// Trạng thái hiển thị: "active" (đang hoạt động), "idle" (đang chờ), "offline" (không hoạt động).
/// - offline: không còn kết nối SignalR nào (tắt màn hình/đóng tab).
/// - active/idle: do client tự báo (idle khi 30' không gọi API).
/// </summary>
public interface IDevicePresenceStore
{
    void Connect(int userId, string deviceId, string connectionId);

    /// <returns>(UserId, DeviceId) của thiết bị bị ảnh hưởng để phát realtime, null nếu không có.</returns>
    (int UserId, string DeviceId)? Disconnect(string connectionId);

    /// <returns>(UserId, DeviceId) của thiết bị vừa đổi trạng thái, null nếu không tìm thấy.</returns>
    (int UserId, string DeviceId)? SetStatus(string connectionId, string status);

    /// <summary>Trạng thái hiển thị của thiết bị: active | idle | offline.</summary>
    string GetStatus(string deviceId);

    IReadOnlyList<string> GetConnectionIds(string deviceId);
}
