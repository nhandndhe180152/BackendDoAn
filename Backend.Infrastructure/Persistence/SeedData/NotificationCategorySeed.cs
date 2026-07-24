using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

public static class NotificationCategorySeed
{
    public static IEnumerable<NotificationCategory> GetCategories()
    {
        return new List<NotificationCategory>
        {
            new NotificationCategory { Id = 1, Name = "Cảnh báo tồn kho thấp", Description = "Thông báo liên quan đến hàng tồn kho thấp cần bổ sung", Color = "#ef4444" },
            new NotificationCategory { Id = 2, Name = "Thu mua lúa", Description = "Thông báo liên quan đến lịch và phiếu thu mua lúa", Color = "#0ea5e9" },
            new NotificationCategory { Id = 3, Name = "Đơn mua hàng", Description = "Thông báo đơn mua hàng hóa khác", Color = "#10b981" },
            new NotificationCategory { Id = 4, Name = "Đơn nhập kho", Description = "Thông báo quy trình nhập kho", Color = "#3b82f6" },
            new NotificationCategory { Id = 5, Name = "Xay xát", Description = "Thông báo quy trình xay xát lúa", Color = "#3b82f6" },
            new NotificationCategory { Id = 6, Name = "Đơn bán hàng", Description = "Thông báo quy trình bán hàng", Color = "#f59e0b" },
            new NotificationCategory { Id = 7, Name = "Đơn xuất kho", Description = "Thông báo quy trình xuất kho", Color = "#10b981" },
            new NotificationCategory { Id = 8, Name = "Điều chuyển kho", Description = "Thông báo điều chuyển nội bộ", Color = "#10b981" },
            new NotificationCategory { Id = 9, Name = "Kiểm kê kho", Description = "Thông báo quá trình kiểm kê", Color = "#10b981" },
            new NotificationCategory { Id = 10, Name = "Kiểm định chất lượng", Description = "Thông báo kiểm tra chất lượng và cảnh báo lô", Color = "#ef4444" },
            new NotificationCategory { Id = 11, Name = "Hệ thống", Description = "Thông báo chung và cảnh báo lỗi từ hệ thống", Color = "#ef4444" }
        };
    }
}
