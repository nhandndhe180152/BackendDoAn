using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Action : EntityCommonBase<int>
{
    /// <summary>
    /// Mã định danh ổn định (bất biến) dùng trong code thay cho Id số của DB.
    /// Vd: CREATE/READ/UPDATE/DELETE/EXPORT/APPROVE. Id số có thể đổi, Code thì không.
    /// </summary>
    public string? Code { get; set; }

    public virtual ICollection<Permission> Permissions { get; set; }
    public virtual ICollection<ActionInMenu> ActionInMenus { get; set; }
}