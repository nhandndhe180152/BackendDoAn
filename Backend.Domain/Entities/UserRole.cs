using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class UserRole : EntityAuditBase<int>
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public virtual User User { get; set; }
    public virtual Role Role { get; set; }
}
