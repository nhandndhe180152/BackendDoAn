using System;

namespace Backend.Application.DTOs.Users;

/// <summary>Một dòng user đọc được từ file Excel/CSV khi import hàng loạt (chưa gán vai trò).</summary>
public class UserImportRowDto
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? IdentityNumber { get; set; }
    public int? Gender { get; set; }
}

/// <summary>Lỗi của một dòng trong lô tạo hàng loạt (Row là số thứ tự 1-based trong lô).</summary>
public class UserBatchRowErrorDto
{
    public int Row { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
}
