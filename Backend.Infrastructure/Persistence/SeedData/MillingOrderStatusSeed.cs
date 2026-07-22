using System;
using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

public static class MillingOrderStatusSeed
{
    /// <summary>
    /// Seed 6 trạng thái lệnh xay.
    /// </summary>
    public static IEnumerable<MillingOrderStatus> GetStatuses()
    {
        return new[]
        {
            new MillingOrderStatus { Id = 1, Code = "DRAFT",           Name = "Nháp",                  Color = "#6B7280", CreatedDate = new DateTime(2026, 1, 1) },
            new MillingOrderStatus { Id = 2, Code = "RESERVED",        Name = "Đã giữ lúa",            Color = "#3B82F6", CreatedDate = new DateTime(2026, 1, 1) },
            new MillingOrderStatus { Id = 3, Code = "IN_PROGRESS",     Name = "Đang xay",              Color = "#F59E0B", CreatedDate = new DateTime(2026, 1, 1) },
            new MillingOrderStatus { Id = 4, Code = "AWAITING_OUTPUT", Name = "Chờ nhập thành phẩm",  Color = "#8B5CF6", CreatedDate = new DateTime(2026, 1, 1) },
            new MillingOrderStatus { Id = 5, Code = "COMPLETED",       Name = "Hoàn tất",              Color = "#10B981", CreatedDate = new DateTime(2026, 1, 1) },
            new MillingOrderStatus { Id = 6, Code = "CANCELLED",       Name = "Hủy",                   Color = "#EF4444", CreatedDate = new DateTime(2026, 1, 1) },
        };
    }
}
