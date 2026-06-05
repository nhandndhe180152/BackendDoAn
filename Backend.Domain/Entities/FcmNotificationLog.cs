using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class FcmNotificationLog : EntityAuditBase<int>
{
    public int? UserId { get; set; }
    public int? UserDeviceId { get; set; }
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;
    public string? DataPayload { get; set; }
    public string? TriggerType { get; set; }
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public bool IsSent { get; set; }
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }

    public virtual User? User { get; set; }
    public virtual UserDevice? UserDevice { get; set; }
}
