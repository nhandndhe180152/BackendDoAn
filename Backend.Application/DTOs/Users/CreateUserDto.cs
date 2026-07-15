using System;

namespace Backend.Application.DTOs.Users;

public class CreateUserDto
{
    /// <summary>Tên đăng nhập (định danh đăng nhập). Tách riêng khỏi Email.</summary>
    public string Username { get; set; } = null!;

    /// <summary>Email chỉ dùng để nhận thông báo/cấp mật khẩu từ hệ thống.</summary>
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public int? Gender { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? IdentityNumber { get; set; }
    public string? AddresDetail { get; set; }
    public int? CreatedBy { get; set; }
    public List<int> Roles { get; set; } = new List<int>();
}
