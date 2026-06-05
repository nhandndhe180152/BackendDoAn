using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Menu : EntityAuditBase<int>
{
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
