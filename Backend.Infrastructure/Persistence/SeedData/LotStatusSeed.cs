using System;
using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

public static class LotStatusSeed
{
    /// <summary>
    /// Seed 6 trạng thái lô.
    /// IsSellable=false: Cách ly, Chờ xử lý.
    /// </summary>
    public static IEnumerable<LotStatus> GetStatuses()
    {
        return new[]
        {
            new LotStatus { Id = 1, Name = "Chờ nhập",       Color = "#6B7280", IsSellable = false, CreatedDate = new DateTime(2026, 1, 1) },
            new LotStatus { Id = 2, Name = "Đang lưu kho",   Color = "#10B981", IsSellable = true,  CreatedDate = new DateTime(2026, 1, 1) },
            new LotStatus { Id = 3, Name = "Chờ xử lý",      Color = "#F59E0B", IsSellable = false, CreatedDate = new DateTime(2026, 1, 1) },
            new LotStatus { Id = 4, Name = "Cách ly",         Color = "#EF4444", IsSellable = false, CreatedDate = new DateTime(2026, 1, 1) },
            new LotStatus { Id = 5, Name = "Đang xay",        Color = "#8B5CF6", IsSellable = false, CreatedDate = new DateTime(2026, 1, 1) },
            new LotStatus { Id = 6, Name = "Đã dùng hết",     Color = "#9CA3AF", IsSellable = false, CreatedDate = new DateTime(2026, 1, 1) },
        };
    }
}
