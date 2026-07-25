using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

public static class NotificationTypeSeed
{
    public static IEnumerable<NotificationType> GetTypes()
    {
        return new List<NotificationType>
        {
            new NotificationType { Id = 1, Name = "Hệ thống", Description = "Thông báo tự động từ hệ thống" }
        };
    }
}
