using System;
using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

public static class StockTransferStatusSeed
{
    /// <summary>
    /// Seed 4 trạng thái phiếu điều chuyển nội bộ.
    /// </summary>
    public static IEnumerable<StockTransferStatus> GetStatuses()
    {
        return new[]
        {
            new StockTransferStatus { Id = 1, Name = "Nháp",          Color = "#6B7280", CreatedDate = new DateTime(2026, 1, 1) },
            new StockTransferStatus { Id = 2, Name = "Đang chuyển",   Color = "#F59E0B", CreatedDate = new DateTime(2026, 1, 1) },
            new StockTransferStatus { Id = 3, Name = "Hoàn tất",      Color = "#10B981", CreatedDate = new DateTime(2026, 1, 1) },
            new StockTransferStatus { Id = 4, Name = "Hủy",           Color = "#EF4444", CreatedDate = new DateTime(2026, 1, 1) },
        };
    }
}
