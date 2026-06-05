using System;

namespace Backend.Application.DTOs.SystemConfigs;

public class ContactInformationDto
{
    public string Hotline { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string WorkingHours { get; set; } = null!;
}
