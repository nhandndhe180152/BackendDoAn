using System;
using Backend.Application.DTOs.SystemConfigs;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface ISystemConfigService : IServiceBase<int, CreateSystemConfigDto, UpdateSystemConfigDto, DTParameter>
{
    Task<string> GetValueByKey(string key);

    /// <summary>
    /// Upsert một cấu hình theo ConfigKey (tạo mới nếu chưa có, cập nhật nếu đã có) rồi làm mới cache.
    /// Dùng cho các cấu hình dạng feature-flag/tham số lưu trong SystemConfig (vd bật/tắt quy tắc cảnh báo).
    /// </summary>
    Task<ApiResponse> SetValueByKeyAsync(string key, string value, string? name = null, string? description = null);

    Task<ApiResponse> GetContactInformationAsync();
    Task<ApiResponse> GetPrivacyPolicyAsync();
    Task<ApiResponse> GetTermOfServiceAsync();
}
