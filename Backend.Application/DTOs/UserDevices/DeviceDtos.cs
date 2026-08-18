using System;

namespace Backend.Application.DTOs.UserDevices;

/// <summary>Đăng ký/cập nhật thiết bị của người dùng đang đăng nhập (gọi sau khi login).</summary>
public class RegisterDeviceDto
{
    public int? UserId { get; set; }
    /// <summary>Định danh thiết bị ổn định do client sinh (localStorage).</summary>
    public string DeviceId { get; set; } = null!;
    public string? DeviceName { get; set; }
    public string? Platform { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
    public string? UserAgent { get; set; }
    /// <summary>FCM token (có thể để trống ở giai đoạn này, bổ sung khi làm FCM).</summary>
    public string? DeviceToken { get; set; }
    /// <summary>Refresh token của phiên hiện tại để liên kết phiên với thiết bị (phục vụ đăng xuất theo thiết bị).</summary>
    public string? RefreshToken { get; set; }
}

/// <summary>Một thiết bị của người dùng hiển thị trong Profile.</summary>
public class MyDeviceDto
{
    public int Id { get; set; }
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? Platform { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
    public string? UserAgent { get; set; }
    /// <summary>Thiết bị còn phiên đăng nhập hiệu lực hay không.</summary>
    public bool HasActiveSession { get; set; }
    /// <summary>Trạng thái realtime: active (đang hoạt động) | idle (đang chờ) | offline (không hoạt động).</summary>
    public string Status { get; set; } = "offline";
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}

/// <summary>Đăng xuất khỏi một thiết bị cụ thể.</summary>
public class LogoutDeviceDto
{
    public int? UserId { get; set; }
    public string DeviceId { get; set; } = null!;
}

/// <summary>Đăng xuất một thiết bị theo Id bản ghi (dùng cho thiết bị không có DeviceId).</summary>
public class LogoutDeviceByIdDto
{
    public int Id { get; set; }
}
