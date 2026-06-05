using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class UserNotification : EntityAuditBase<int>
    {
        public int UserId { get; set; }
        public int NotificationId { get; set; }
        public bool IsRead { get; set; }
        public virtual User User { get; set; } 
        public virtual Notification Notification { get; set; }
    }
