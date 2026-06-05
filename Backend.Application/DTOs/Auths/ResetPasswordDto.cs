using System;

namespace Backend.Application.DTOs.Auths;

public class ResetPasswordDto : VerifyCodeDto
{
    public string NewPassword { get; set; } = null!;
    public string ConfirmPassword { get; set; } = null!;
}
