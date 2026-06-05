using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Permission : EntityAuditBase<int>
{
    public int MenuId { get; set; }
    public int RoleId { get; set; }
    public int ActionId { get; set; }
    public virtual Menu Menu { get; set; }
    public virtual Role Role { get; set; }
    public virtual Action Action { get; set; }
}
