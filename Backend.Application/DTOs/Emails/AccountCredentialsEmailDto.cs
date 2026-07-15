namespace Backend.Application.DTOs.Emails;

/// <summary>
/// Model cho email bàn giao tài khoản khi admin tạo mới (username + mật khẩu tạm).
/// </summary>
public class AccountCredentialsEmailDto
{
    public string SystemName { get; set; } = null!;
    public string LogoUrl { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string LoginLink { get; set; } = null!;
    public string Year { get; set; } = null!;
}
