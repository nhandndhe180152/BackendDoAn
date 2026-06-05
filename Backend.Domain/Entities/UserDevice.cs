using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class UserDevice : EntityAuditBase<int>
{
    public int UserId { get; set; }
    public string? DeviceName { get; set; }
    public string? Platform { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
    public string? DeviceToken { get; set; }
    public string? UserAgent { get; set; }
    public virtual User User { get; set; }
    public virtual ICollection<UserSession> UserSessions { get; set; }
}
