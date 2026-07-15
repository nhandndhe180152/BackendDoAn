namespace Backend.Application.DTOs.Emails;

/// <summary>
/// Model cho email gửi mật khẩu mới khi người dùng dùng tính năng "Quên mật khẩu".
/// </summary>
public class ResetPasswordEmailDto
{
    public string SystemName { get; set; } = null!;
    public string LogoUrl { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string LoginLink { get; set; } = null!;
    public string Year { get; set; } = null!;
}
