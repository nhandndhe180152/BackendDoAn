using System;

namespace Backend.Domain.Aggregates;

public class AuditLogAggregate
{
    public int Id { get; set; }
    public string Action { get; set; } = null!;
    public string ActionName { get; set; } = null!;
    public string TargetType { get; set; } = null!;
    public string TargetTypeName { get; set; } = null!;
    public string? TargetId { get; set; }
    public string? Description { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedUserName { get; set; }
    public int? CreatedUserId { get; set; }
    public bool HasDataChanges => !string.IsNullOrEmpty(DataBefore) || !string.IsNullOrEmpty(DataAfter);
    public string? DataBefore { get; set; }
    public string? DataAfter { get; set; }
}
