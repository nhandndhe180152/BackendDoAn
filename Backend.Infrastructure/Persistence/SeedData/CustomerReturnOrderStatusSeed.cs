using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

public static class CustomerReturnOrderStatusSeed
{
    public static List<CustomerReturnOrderStatus> GetStatuses()
    {
        return new List<CustomerReturnOrderStatus>
        {
            new CustomerReturnOrderStatus { Id = 1, Code = "DRAFT", Name = "Nháp", Color = "#6B7280", CreatedDate = new System.DateTime(2026, 1, 1) },
            new CustomerReturnOrderStatus { Id = 2, Code = "APPROVED", Name = "Đã duyệt", Color = "#3B82F6", CreatedDate = new System.DateTime(2026, 1, 1) },
            new CustomerReturnOrderStatus { Id = 3, Code = "INSPECTED", Name = "Đã kiểm định", Color = "#F59E0B", CreatedDate = new System.DateTime(2026, 1, 1) },
            new CustomerReturnOrderStatus { Id = 4, Code = "CONFIRMED", Name = "Hoàn tất", Color = "#10B981", CreatedDate = new System.DateTime(2026, 1, 1) },
            new CustomerReturnOrderStatus { Id = 5, Code = "CANCELLED", Name = "Đã hủy", Color = "#EF4444", CreatedDate = new System.DateTime(2026, 1, 1) },
            new CustomerReturnOrderStatus { Id = 6, Code = "PENDING_APPROVAL", Name = "Chờ duyệt", Color = "#8B5CF6", CreatedDate = new System.DateTime(2026, 1, 1) },
            new CustomerReturnOrderStatus { Id = 7, Code = "RECEIVED", Name = "Đã nhận hàng", Color = "#06B6D4", CreatedDate = new System.DateTime(2026, 1, 1) },
            new CustomerReturnOrderStatus { Id = 8, Code = "REJECTED", Name = "Từ chối", Color = "#DC2626", CreatedDate = new System.DateTime(2026, 1, 1) }
        };
    }
}
