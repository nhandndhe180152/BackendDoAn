using System;
using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

public static class PaddyPurchaseScheduleStatusSeed
{
    /// <summary>
    /// Seed 7 trạng thái lịch thu mua lúa.
    /// </summary>
    public static IEnumerable<PaddyPurchaseScheduleStatus> GetStatuses()
    {
        return new[]
        {
            new PaddyPurchaseScheduleStatus { Id = 1, Code = "NEW",               Name = "Mới tạo",           Color = "#6B7280", CreatedDate = new DateTime(2026, 1, 1) },
            new PaddyPurchaseScheduleStatus { Id = 2, Code = "CONFIRMED",         Name = "Đã xác nhận",       Color = "#3B82F6", CreatedDate = new DateTime(2026, 1, 1) },
            new PaddyPurchaseScheduleStatus { Id = 3, Code = "COLLECTING",        Name = "Đang đi thu",       Color = "#F59E0B", CreatedDate = new DateTime(2026, 1, 1) },
            new PaddyPurchaseScheduleStatus { Id = 4, Code = "WEIGHED",           Name = "Đã cân hàng",       Color = "#8B5CF6", CreatedDate = new DateTime(2026, 1, 1) },
            new PaddyPurchaseScheduleStatus { Id = 5, Code = "STOCKED",           Name = "Đã nhập kho",       Color = "#10B981", CreatedDate = new DateTime(2026, 1, 1) },
            new PaddyPurchaseScheduleStatus { Id = 6, Code = "CANCELLED",         Name = "Hủy",               Color = "#EF4444", CreatedDate = new DateTime(2026, 1, 1) },
            new PaddyPurchaseScheduleStatus { Id = 7, Code = "PARTIALLY_STOCKED", Name = "Nhập một phần",     Color = "#3B82F6", CreatedDate = new DateTime(2026, 1, 1) },
        };
    }
}
