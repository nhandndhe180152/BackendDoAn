using System;

namespace Backend.Application.DTOs.Auths;

public class ClientAppleLoginDto
{
    public string AppleId { get; set; } = null!;
    public string? Email { get; set; }
}
