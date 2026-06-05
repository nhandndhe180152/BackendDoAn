using System;

namespace Backend.Application.DTOs.Auths;

public class VerifyCodeDto : ForgotPasswordDto
{
    public string Purpose { get; set; }
    public string Code { get; set; }
}
