using System;
using Backend.Application.DTOs.UserDevices;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IUserDeviceService : IServiceBase<int, CreateUserDeviceDto, UpdateUserDeviceDto, DTParameter>
{
    Task<ApiResponse> AddDeviceToken(CreateUserDeviceDto dto);
    Task<ApiResponse> DeleteDeviceToken(DeleteUserDeviceDto dto);

    /// <summary>Đăng ký/cập nhật thiết bị của user đang đăng nhập và liên kết phiên hiện tại với thiết bị.</summary>
    Task<ApiResponse> RegisterDeviceAsync(RegisterDeviceDto dto);

    /// <summary>Danh sách thiết bị đã đăng ký của user.</summary>
    Task<ApiResponse> GetMyDevicesAsync(int userId);

    /// <summary>Đăng xuất khỏi một thiết bị (thu hồi phiên của thiết bị + xóa đăng ký).</summary>
    Task<ApiResponse> LogoutDeviceAsync(int userId, string deviceId);

    /// <summary>Đăng xuất khỏi tất cả thiết bị khác, giữ lại thiết bị hiện tại.</summary>
    Task<ApiResponse> LogoutOtherDevicesAsync(int userId, string currentDeviceId);
}
