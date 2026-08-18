using System;
using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

public static class PurchaseOrderStatusSeed
{
    /// <summary>
    /// Seed 5 trạng thái đơn mua.
    /// </summary>
    public static IEnumerable<PurchaseOrderStatus> GetStatuses()
    {
        return new[]
        {
            new PurchaseOrderStatus { Id = 1, Name = "Nháp",              Code = "DRAFT",              Color = "#6B7280", CreatedDate = new DateTime(2026, 1, 1) },
            new PurchaseOrderStatus { Id = 2, Name = "Đã xác nhận",       Code = "CONFIRMED",          Color = "#3B82F6", CreatedDate = new DateTime(2026, 1, 1) },
            new PurchaseOrderStatus { Id = 3, Name = "Nhận một phần",    Code = "PARTIALLY_RECEIVED", Color = "#F59E0B", CreatedDate = new DateTime(2026, 1, 1) },
            new PurchaseOrderStatus { Id = 4, Name = "Đã nhận hàng",      Code = "RECEIVED",           Color = "#10B981", CreatedDate = new DateTime(2026, 1, 1) },
            new PurchaseOrderStatus { Id = 5, Name = "Đã hủy",           Code = "CANCELLED",          Color = "#EF4444", CreatedDate = new DateTime(2026, 1, 1) },
        };
    }
}
