using System;

namespace Backend.Application.DTOs.Auths;

public class ForgotPasswordDto
{
    public string Email { get; set; }
    public bool IsClientRequest { get; set; } = false;
}
