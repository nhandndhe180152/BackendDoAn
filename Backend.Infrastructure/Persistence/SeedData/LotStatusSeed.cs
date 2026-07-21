using System;
using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

public static class LotStatusSeed
{
    /// <summary>
    /// Seed 6 trạng thái lô hàng.
    /// IsSellable=true: chỉ "Dạng lưu kho" (lô đã nhập, chưa xử lý, đủ điều kiện xuất bán).
    /// IsSellable=false: tất cả trạng thái còn lại (chưa nhập / đang xử lý / cách ly / đang xay / đã dùng hết).
    /// → Đã được chốt lại theo nghiệp vụ (PR review 10/07/2026); lệch doc cũ chỉ nêu Cách ly + Chờ xử lý.
    /// </summary>
    public static IEnumerable<LotStatus> GetStatuses()
    {
        return new[]
        {
            new LotStatus { Id = 1, Name = "Chờ nhập",       Code = "PENDING_INBOUND", Color = "#6B7280", IsSellable = false, CreatedDate = new DateTime(2026, 1, 1) },
            new LotStatus { Id = 2, Name = "Đang lưu kho",   Code = "IN_STOCK",        Color = "#10B981", IsSellable = true,  CreatedDate = new DateTime(2026, 1, 1) },
            new LotStatus { Id = 3, Name = "Chờ xử lý",      Code = "PROCESSING",      Color = "#F59E0B", IsSellable = false, CreatedDate = new DateTime(2026, 1, 1) },
            new LotStatus { Id = 4, Name = "Cách ly",         Code = "QUARANTINE",      Color = "#EF4444", IsSellable = false, CreatedDate = new DateTime(2026, 1, 1) },
            new LotStatus { Id = 5, Name = "Đang xay",        Code = "MILLING",         Color = "#8B5CF6", IsSellable = false, CreatedDate = new DateTime(2026, 1, 1) },
            new LotStatus { Id = 6, Name = "Đã dùng hết",     Code = "DEPLETED",        Color = "#9CA3AF", IsSellable = false, CreatedDate = new DateTime(2026, 1, 1) },
        };
    }
}
