using System;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.AuditLogs;

public class AuditLogDetailDto
{
    public int Id { get; set; }
    public string Action { get; set; } = null!;
    public string ActionName { get; set; } = null!;
    public string TargetType { get; set; } = null!;
    public string TargetTypeName { get; set; } = null!;
    public string? TargetId { get; set; }
    public string? DataBefore { get; set; }
    public string? DataAfter { get; set; }
    public string? Description { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedDate { get; set; }
    public DataItem<int>? CreatedUser { get; set; }
}
