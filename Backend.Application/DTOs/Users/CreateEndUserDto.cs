using System;

namespace Backend.Application.DTOs.Users;

public class CreateEndUserDto
{
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string Email { get; set; } = null!;
    public string IdentityNumber { get; set; } = null!;
    public string? AddressDetail { get; set; }
    public int? CreatedBy { get; set; }
    //update
    public string DateOfBirth { get; set; }
    public int Gender { get; set; }
}
