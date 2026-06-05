using System;

namespace Backend.Application.DTOs.Emails;

public class AdminForgotPasswordEmailDto
{
    public string FullName { get; set; }
    public string ActiveLink { get; set; }
    public string ValidExpired { get; set; }
    public string DateTime { get; set; }
    public string Link { get; set; }
    public string OtpCode { get; set; }
}
