using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Role : EntityCommonBase<int>
{
    /// <summary>
    /// Mã định danh ổn định (bất biến) dùng trong code thay cho Id số của DB. Vd: ADMIN/END_USER.
    /// </summary>
    public string? Code { get; set; }

    public virtual ICollection<UserRole> UserRoles { get; set; } = new HashSet<UserRole>();
    public virtual ICollection<Permission> Permissions { get; set; } = new HashSet<Permission>();
}
