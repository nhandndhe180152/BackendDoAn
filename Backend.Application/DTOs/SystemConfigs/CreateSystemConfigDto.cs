using System;

namespace Backend.Application.DTOs.SystemConfigs;

public class CreateSystemConfigDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string ConfigKey { get; set; } = null!;
    public string ConfigValue { get; set; } = null!;
    public int? CreatedBy { get; set; }
}
