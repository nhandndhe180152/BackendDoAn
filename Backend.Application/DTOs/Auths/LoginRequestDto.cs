using System;

namespace Backend.Application.DTOs.Auths;

public class LoginRequestDto
{
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
}
