using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class UserSession : EntityAuditBase<int>
{
    public int UserId { get; set; }
    public string RefreshToken { get; set; } = null!;
    public string AccessTokenJti { get; set; } = null!;
    public int? UserDeviceId { get; set; }
    public bool IsUsed { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime ExpirationDate { get; set; }
    public virtual User User { get; set; }
    public virtual UserDevice UserDevice { get; set; }
}