using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class UserStatus : EntityCommonBase<int>
{
    public string Color { get; set; } = null!;
    public virtual ICollection<User> Users { get; set; }
}
