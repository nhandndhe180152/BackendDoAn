using System;
using Backend.Application.DTOs.FileUploads;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.Users;

public class UserProfileDto
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public int? Gender { get; set; }
    public string? PhoneNumber { get; set; }
    public string? IdentityNumber { get; set; }
    public string? AddresDetail { get; set; }
    public DataItem<int> UserStatus { get; set; }
    public List<DataItem<int>> UserRoles { get; set; }
    public FileUploadDetailDto? Avatar { get; set; }
}
