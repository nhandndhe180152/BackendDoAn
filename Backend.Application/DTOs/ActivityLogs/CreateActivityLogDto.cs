using System;

namespace Backend.Application.DTOs.ActivityLogs;

public class CreateActivityLogDto
{
    public string Action { get; set; } = null!;
    public string? Description { get; set; }
    public int? CreatedBy { get; set; }
}
