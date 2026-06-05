using System;

namespace Backend.Application.DTOs.Auths;

public class ClientGoogleLoginDto
{
    public string GoogleId { get; set; } = null!;
    public string Email { get; set; } = null!;
}
