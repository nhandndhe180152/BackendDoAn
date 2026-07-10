using System;
using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

public static class SalesOrderStatusSeed
{
    /// <summary>
    /// Seed 8 trạng thái đơn bán offline.
    /// </summary>
    public static IEnumerable<SalesOrderStatus> GetStatuses()
    {
        return new[]
        {
            new SalesOrderStatus { Id = 1, Name = "Mới tạo",          Color = "#6B7280", CreatedDate = new DateTime(2026, 1, 1) },
            new SalesOrderStatus { Id = 2, Name = "Chờ xác nhận",     Color = "#3B82F6", CreatedDate = new DateTime(2026, 1, 1) },
            new SalesOrderStatus { Id = 3, Name = "Đã giữ hàng",      Color = "#8B5CF6", CreatedDate = new DateTime(2026, 1, 1) },
            new SalesOrderStatus { Id = 4, Name = "Chờ xay",          Color = "#F59E0B", CreatedDate = new DateTime(2026, 1, 1) },
            new SalesOrderStatus { Id = 5, Name = "Đang chuẩn bị",    Color = "#06B6D4", CreatedDate = new DateTime(2026, 1, 1) },
            new SalesOrderStatus { Id = 6, Name = "Đang giao",        Color = "#F97316", CreatedDate = new DateTime(2026, 1, 1) },
            new SalesOrderStatus { Id = 7, Name = "Hoàn tất",         Color = "#10B981", CreatedDate = new DateTime(2026, 1, 1) },
            new SalesOrderStatus { Id = 8, Name = "Hủy",              Color = "#EF4444", CreatedDate = new DateTime(2026, 1, 1) },
        };
    }
}
