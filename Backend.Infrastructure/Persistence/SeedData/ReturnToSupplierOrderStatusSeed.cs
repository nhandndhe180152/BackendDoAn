using System;
using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

/// <summary>
/// Seed 4 trạng thái đơn trả hàng về nhà cung cấp (ReturnToSupplierOrder).
/// Flow: Chờ duyệt → Đã duyệt → Hoàn thành (hoặc Đã huỷ).
/// </summary>
public static class ReturnToSupplierOrderStatusSeed
{
    public static IEnumerable<ReturnToSupplierOrderStatus> GetStatuses()
    {
        return new[]
        {
            new ReturnToSupplierOrderStatus { Id = 1, Name = "Chờ duyệt",  Color = "#F59E0B", CreatedDate = new DateTime(2026, 1, 1) },
            new ReturnToSupplierOrderStatus { Id = 2, Name = "Đã duyệt",   Color = "#3B82F6", CreatedDate = new DateTime(2026, 1, 1) },
            new ReturnToSupplierOrderStatus { Id = 3, Name = "Hoàn thành", Color = "#16A34A", CreatedDate = new DateTime(2026, 1, 1) },
            new ReturnToSupplierOrderStatus { Id = 4, Name = "Đã huỷ",     Color = "#EF4444", CreatedDate = new DateTime(2026, 1, 1) },
        };
    }
}
