using System.Collections.Generic;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

public static class StockTakeStatusSeed
{
    public static List<StockTakeStatus> GetStockTakeStatuses()
    {
        return new List<StockTakeStatus>
        {
            new StockTakeStatus
            {
                Id = 1,
                Code = "Draft",
                Name = "Mới tạo",
                Color = "#ff9500"
            },
            new StockTakeStatus
            {
                Id = 2,
                Code = "Submitted",
                Name = "Đã gửi yêu cầu",
                Color = "#007bff"
            },
            new StockTakeStatus
            {
                Id = 3,
                Code = "Approved",
                Name = "Đã duyệt",
                Color = "#00b315"
            },
            new StockTakeStatus
            {
                Id = 4,
                Code = "Rejected",
                Name = "Từ chối",
                Color = "#ff0000"
            }
        };
    }
}
