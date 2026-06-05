using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class UserVerificationToken : EntityAuditBase<int>
{
    public int UserId { get; set; }
    public string Code { get; set; } = null!;
    public string Purpose { get; set; } = null!;
    public bool IsUsed { get; set; }
    public DateTime ExpirationDate { get; set; }
    public virtual User User { get; set; }
}
