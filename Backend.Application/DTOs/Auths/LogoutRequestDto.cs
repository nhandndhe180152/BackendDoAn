using System;

namespace Backend.Application.DTOs.Auths;

public class LogoutRequestDto
{
    public string RefreshToken { get; set; } = null!;
}
