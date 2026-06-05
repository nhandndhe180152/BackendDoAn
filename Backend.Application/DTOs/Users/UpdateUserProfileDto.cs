using System;

namespace Backend.Application.DTOs.Users;

public class UpdateUserProfileDto
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public int? Gender { get; set; }
    public string? PhoneNumber { get; set; }
    public string? IdentityNumber { get; set; }
    public string? AddresDetail { get; set; }
    public int? AvatarId { get; set; }
}
