using System;

namespace Backend.Application.DTOs.SystemConfigs;

public class SystemConfigDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string ConfigKey { get; set; } = null!;
    public string ConfigValue { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
}