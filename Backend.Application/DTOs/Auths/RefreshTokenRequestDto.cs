using System;

namespace Backend.Application.DTOs.Auths;

public class RefreshTokenRequestDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = null!;
}
