using System;

namespace Backend.Application.DTOs.Auths;

public class RefreshTokenResponseDto
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
}
