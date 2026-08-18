using System;
using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

/// <summary>
/// Seed 6 trạng thái phiếu xuất kho (OutboundOrder).
/// </summary>
public static class OutboundOrderStatusSeed
{
    public static IEnumerable<OutboundOrderStatus> GetStatuses()
    {
        return new[]
        {
            new OutboundOrderStatus { Id = 1,  Name = "Nháp",              Code = "DRAFT",              Color = "#6B7280", CreatedDate = new DateTime(2026, 1, 1) },
            new OutboundOrderStatus { Id = 2,  Name = "Đang lấy hàng",     Code = "PICKING",            Color = "#3B82F6", CreatedDate = new DateTime(2026, 1, 1) },
            new OutboundOrderStatus { Id = 3,  Name = "Đã đóng gói",       Code = "PACKED",             Color = "#8B5CF6", CreatedDate = new DateTime(2026, 1, 1) },
            new OutboundOrderStatus { Id = 4,  Name = "Đang giao hàng",    Code = "DISPATCHED",         Color = "#F97316", CreatedDate = new DateTime(2026, 1, 1) },
            new OutboundOrderStatus { Id = 5,  Name = "Hoàn thành",        Code = "COMPLETED",          Color = "#10B981", CreatedDate = new DateTime(2026, 1, 1) },
            new OutboundOrderStatus { Id = 6,  Name = "Đã hủy",            Code = "CANCELLED",          Color = "#EF4444", CreatedDate = new DateTime(2026, 1, 1) },
            new OutboundOrderStatus { Id = 7,  Name = "Giao hàng thất bại", Code = "DELIVERY_FAILED",    Color = "#EF4444", CreatedDate = new DateTime(2026, 1, 1) },
        };
    }
}
