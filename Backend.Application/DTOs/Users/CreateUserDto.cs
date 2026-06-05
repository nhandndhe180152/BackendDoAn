using System;

namespace Backend.Application.DTOs.Users;

public class CreateUserDto
{
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public int? Gender { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? IdentityNumber { get; set; }
    public string? AddresDetail { get; set; }
    public int? CreatedBy { get; set; }
    public List<int> Roles { get; set; } = new List<int>();
}
