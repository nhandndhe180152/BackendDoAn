using System;

namespace Backend.Application.DTOs.Emails;

public class UserActivationDto
{
    public string FullName { get; set; } = null!;
    public string ActiveLink { get; set; } = null!;
    public string ValidExpired { get; set; } = null!;
    public string DateTime { get; set; } = null!;
    public string Link { get; set; } = null!;

    // Brand hệ thống (lấy từ SystemConfig) để email dùng tên + logo mới.
    public string SystemName { get; set; } = null!;
    public string LogoUrl { get; set; } = null!;
}
