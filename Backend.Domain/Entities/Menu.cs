using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Menu : EntityAuditBase<int>
{
    /// <summary>
    /// Mã định danh ổn định (bất biến) dùng cho phân quyền thay cho Id số của DB.
    /// Vd: PRODUCT/NOTIFICATION/SYSTEM_CONFIG. Khớp với tên thành viên enum Enums.Menu.
    /// </summary>
    public string? Code { get; set; }

    public int? ParentId { get; set; }
    public string TreeIds { get; set; } = null!;
    public string MenuType { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Url { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? ClassName { get; set; }
    public int SortOrder { get; set; } = 1;
    public bool IsAdminOnly { get; set; }
    public virtual ICollection<Permission> Permissions { get; set; }
    public virtual ICollection<ActionInMenu> ActionInMenus { get; set; }
}
