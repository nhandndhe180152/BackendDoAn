using System.Threading.Tasks;
using Backend.API.Hubs;
using Backend.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Backend.API.Utilities;

/// <summary>Cài đặt <see cref="IDevicePresenceNotifier"/> dùng SignalR (DevicePresenceHub).</summary>
public class DevicePresenceNotifier : IDevicePresenceNotifier
{
    private readonly IHubContext<DevicePresenceHub> _hubContext;
    private readonly IDevicePresenceStore _store;

    public DevicePresenceNotifier(IHubContext<DevicePresenceHub> hubContext, IDevicePresenceStore store)
    {
        _hubContext = hubContext;
        _store = store;
    }

    public Task NotifyDevicesChangedAsync(int userId)
        => _hubContext.Clients.Group($"user-{userId}").SendAsync("DevicesChanged");

    public async Task ForceLogoutDeviceAsync(string deviceId)
    {
        var connections = _store.GetConnectionIds(deviceId);
        if (connections.Count > 0)
            await _hubContext.Clients.Clients(connections).SendAsync("ForceLogout");
    }
}
