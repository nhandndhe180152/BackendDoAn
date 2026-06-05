using System;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.ActivityLogs;

public class ActivityLogDetailDto
{
    public int Id { get; set; }
    public string Action { get; set; } = null!;
    public string? Description { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedDate { get; set; }
    public DataItem<int>? CreatedUser { get; set; }
}