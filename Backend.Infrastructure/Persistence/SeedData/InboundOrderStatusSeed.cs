using System;
using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

/// <summary>
/// Seed 8 trạng thái phiếu nhập kho (InboundOrder).
/// </summary>
public static class InboundOrderStatusSeed
{
    public static IEnumerable<InboundOrderStatus> GetStatuses()
    {
        return new[]
        {
            new InboundOrderStatus { Id = 1, Name = "Nháp",            Code = "DRAFT",              Color = "#6B7280", CreatedDate = new DateTime(2026, 1, 1) },
            new InboundOrderStatus { Id = 2, Name = "Chờ duyệt",       Code = "SUBMITTED",          Color = "#3B82F6", CreatedDate = new DateTime(2026, 1, 1) },
            new InboundOrderStatus { Id = 3, Name = "Đã duyệt",        Code = "APPROVED",           Color = "#8B5CF6", CreatedDate = new DateTime(2026, 1, 1) },
            new InboundOrderStatus { Id = 4, Name = "Từ chối",         Code = "REJECTED",           Color = "#EF4444", CreatedDate = new DateTime(2026, 1, 1) },
            new InboundOrderStatus { Id = 5, Name = "Đang nhận hàng",   Code = "RECEIVING",          Color = "#06B6D4", CreatedDate = new DateTime(2026, 1, 1) },
            new InboundOrderStatus { Id = 6, Name = "Nhận một phần",    Code = "PARTIALLY_RECEIVED", Color = "#F59E0B", CreatedDate = new DateTime(2026, 1, 1) },
            new InboundOrderStatus { Id = 7, Name = "Đã nhận hàng",     Code = "CONFIRMED",          Color = "#10B981", CreatedDate = new DateTime(2026, 1, 1) },
            new InboundOrderStatus { Id = 8, Name = "Đã hủy",           Code = "CANCELLED",          Color = "#EF4444", CreatedDate = new DateTime(2026, 1, 1) },
        };
    }
}
