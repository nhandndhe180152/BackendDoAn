using System;
using System.Threading.Tasks;
using Backend.Application.Interfaces;
using Backend.Share.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Backend.API.Hubs;

/// <summary>
/// Hub theo dõi hiện diện thiết bị. Client kết nối kèm ?deviceId=... và access_token.
/// - offline: server tự phát hiện khi ngắt kết nối (tắt màn hình/đóng tab).
/// - active/idle: client gọi <see cref="ReportStatus"/> ("active" | "idle").
/// Sự kiện gửi về client: "DevicesChanged" (làm mới danh sách), "ForceLogout" (đăng xuất tại chỗ).
/// </summary>
[Authorize]
public class DevicePresenceHub : Hub
{
    private readonly IDevicePresenceStore _store;

    public DevicePresenceHub(IDevicePresenceStore store)
    {
        _store = store;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var deviceId = GetDeviceId();
        if (userId > 0 && !string.IsNullOrWhiteSpace(deviceId))
        {
            _store.Connect(userId, deviceId, Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(userId));
            await Clients.Group(GroupName(userId)).SendAsync("DevicesChanged");
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var affected = _store.Disconnect(Context.ConnectionId);
        if (affected != null)
            await Clients.Group(GroupName(affected.Value.UserId)).SendAsync("DevicesChanged");
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Client báo trạng thái hoạt động: "active" hoặc "idle".</summary>
    public async Task ReportStatus(string status)
    {
        var affected = _store.SetStatus(Context.ConnectionId, status);
        if (affected != null)
            await Clients.Group(GroupName(affected.Value.UserId)).SendAsync("DevicesChanged");
    }

    private static string GroupName(int userId) => $"user-{userId}";

    private int GetUserId()
    {
        var raw = Context.User?.FindFirst(x => x.Type == ClaimNames.ID)?.Value;
        return int.TryParse(raw, out var id) ? id : 0;
    }

    private string GetDeviceId()
    {
        var http = Context.GetHttpContext();
        return http?.Request.Query["deviceId"].ToString() ?? string.Empty;
    }
}
