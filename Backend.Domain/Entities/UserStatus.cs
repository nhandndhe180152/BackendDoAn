using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class UserStatus : EntityCommonBase<int>
{
    /// <summary>
    /// Mã định danh ổn định (bất biến) dùng trong code thay cho Id số của DB.
    /// Vd: NotActivated/Actived/Locked/Deactivated.
    /// </summary>
    public string? Code { get; set; }

    public string Color { get; set; } = null!;
    public virtual ICollection<User> Users { get; set; }
}
