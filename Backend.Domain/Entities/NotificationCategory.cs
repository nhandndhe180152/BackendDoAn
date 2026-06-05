using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class NotificationCategory : EntityCommonBase<int>
{
    public string Color { get; set; } = null!;
    public virtual ICollection<Notification> Notifications { get; set; }
}
