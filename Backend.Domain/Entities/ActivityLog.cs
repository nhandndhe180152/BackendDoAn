using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class ActivityLog : EntityAuditBase<int>
{
    public string ActivityLogType { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string TargetType { get; set; } = null!;
    public string? TargetId { get; set; }
    public string? Description { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
