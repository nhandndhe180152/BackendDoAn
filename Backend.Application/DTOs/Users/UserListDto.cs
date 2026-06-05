using System;

namespace Backend.Application.DTOs.Users;

public class UserListDto
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public int? Gender { get; set; }
    public string? PhoneNumber { get; set; }
    public int AccessFailedCount { get; set; }
    public bool LockEnabled { get; set; }
    public DateTime? LockEndDate { get; set; }
    public int UserStatusId { get; set; }
    public string UserStatusName { get; set; } = null!;
    public int? AvatarId { get; set; }
    public string? AvatarKey { get; set; }
    public string? AvatarUrl { get; set; }
    public string? IdentityNumber { get; set; }
    public string? AddressDetail { get; set; }
    public DateTime CreatedDate { get; set; }

    public DateTime? DateOfBirth { get; set; }
}
