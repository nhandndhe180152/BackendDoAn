using System;
using Backend.Application.DTOs.FileUploads;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.Users;

public class UserDetailDto
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public int? Gender { get; set; }
    public string? PhoneNumber { get; set; }
    public string? IdentityNumber { get; set; }
    public string? AddressDetail { get; set; }
    public int AccessFailedCount { get; set; }
    public bool LockEnabled { get; set; }
    public DateTime? LockEndDate { get; set; }
    public DataItem<int> UserStatus { get; set; } = new DataItem<int>();
    public virtual FileUploadDetailDto? Avatar { get; set; }
    public List<DataItem<int>>? Roles { get; set; } = new List<DataItem<int>>();
    public DateTime CreatedDate { get; set; }

    public DateTime? DateOfBirth { get; set; }

}
