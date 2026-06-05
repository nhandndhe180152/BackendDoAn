using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Role : EntityCommonBase<int>
{
    public virtual ICollection<UserRole> UserRoles { get; set; } = new HashSet<UserRole>();
    public virtual ICollection<Permission> Permissions { get; set; } = new HashSet<Permission>();
}
