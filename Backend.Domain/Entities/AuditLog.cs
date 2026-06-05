using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class AuditLog : EntityAuditBase<int>
{
    public string Action { get; set; } = null!;
    public string TargetType { get; set; } = null!;
    public string? TargetId { get; set; }
    public string? DataBefore { get; set; }
    public string? DataAfter { get; set; }
    public string? Description { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
