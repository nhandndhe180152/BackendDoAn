using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.DTParameters;

namespace Backend.Domain.Interfaces.Repositories;

public interface ISalesOrderRepository : IRepositoryBase<SalesOrder, int>
{
    Task<SalesOrder?> GetByIdDetailAsync(int id);
    
    /// <summary>
    /// Trang danh sách đơn bán. [statusId], [channel], [customerId], [warehouseId], [fromDate], [toDate] lọc ngay trên DB.
    /// </summary>
    Task<List<SalesOrder>> GetPagedListAsync(
        string? keyword, int skip, int take, int? statusId = null, string? channel = null,
        int? customerId = null, int? warehouseId = null, DateTime? fromDate = null, DateTime? toDate = null);

    Task<int> CountAsync(
        string? keyword, int? statusId = null, string? channel = null,
        int? customerId = null, int? warehouseId = null, DateTime? fromDate = null, DateTime? toDate = null);
}
