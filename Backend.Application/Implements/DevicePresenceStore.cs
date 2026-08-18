using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using Backend.Application.Interfaces;

namespace Backend.Application.Implements;

/// <summary>
/// Cài đặt in-memory cho <see cref="IDevicePresenceStore"/>. Đăng ký dạng Singleton.
/// Phù hợp deploy 1 instance (vd Render). Nếu scale nhiều instance cần backplane (Redis).
/// </summary>
public class DevicePresenceStore : IDevicePresenceStore
{
    private sealed class Presence
    {
        public int UserId { get; set; }
        public HashSet<string> Connections { get; } = new();
        public string Status { get; set; } = "active";
    }

    private readonly object _lock = new();
    private readonly Dictionary<string, Presence> _devices = new();     // deviceId -> presence
    private readonly Dictionary<string, string> _connToDevice = new();  // connectionId -> deviceId

    public void Connect(int userId, string deviceId, string connectionId)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(connectionId)) return;
        lock (_lock)
        {
            if (!_devices.TryGetValue(deviceId, out var p))
            {
                p = new Presence { UserId = userId };
                _devices[deviceId] = p;
            }
            p.UserId = userId;
            p.Connections.Add(connectionId);
            if (p.Connections.Count == 1) p.Status = "active"; // vừa online lại
            _connToDevice[connectionId] = deviceId;
        }
    }

    public (int UserId, string DeviceId)? Disconnect(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId)) return null;
        lock (_lock)
        {
            if (!_connToDevice.TryGetValue(connectionId, out var deviceId)) return null;
            _connToDevice.Remove(connectionId);
            if (_devices.TryGetValue(deviceId, out var p))
            {
                p.Connections.Remove(connectionId);
                return (p.UserId, deviceId);
            }
            return null;
        }
    }

    public (int UserId, string DeviceId)? SetStatus(string connectionId, string status)
    {
        if (string.IsNullOrWhiteSpace(connectionId) || string.IsNullOrWhiteSpace(status)) return null;
        lock (_lock)
        {
            if (!_connToDevice.TryGetValue(connectionId, out var deviceId)) return null;
            if (_devices.TryGetValue(deviceId, out var p))
            {
                p.Status = status == "idle" ? "idle" : "active";
                return (p.UserId, deviceId);
            }
            return null;
        }
    }

    public string GetStatus(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return "offline";
        lock (_lock)
        {
            if (_devices.TryGetValue(deviceId, out var p) && p.Connections.Count > 0)
                return p.Status;
            return "offline";
        }
    }

    public IReadOnlyList<string> GetConnectionIds(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return Array.Empty<string>();
        lock (_lock)
        {
            if (_devices.TryGetValue(deviceId, out var p))
                return p.Connections.ToList();
            return Array.Empty<string>();
        }
    }
}
