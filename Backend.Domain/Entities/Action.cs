using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Action : EntityCommonBase<int>
{
    public virtual ICollection<Permission> Permissions { get; set; }
    public virtual ICollection<ActionInMenu> ActionInMenus { get; set; }
}