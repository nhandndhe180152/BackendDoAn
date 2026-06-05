using System;

namespace Backend.Application.DTOs.Auths;

public class ResendActivationMailDto
{
    public required string Email { get; set; }
    public required string VerificationCode { get; set; }
}
