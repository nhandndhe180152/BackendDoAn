using System;
using System.Text.Json.Serialization;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class NotificationType : EntityCommonBase<int>
{
    [JsonIgnore]
    public virtual ICollection<Notification> Notifications { get; set; }
}
