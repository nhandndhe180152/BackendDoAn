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
    /// Trang danh sách đơn bán. [statusId] và [channel] lọc ngay trên DB để số
    /// trang luôn khớp với bộ lọc (tránh lọc client-side chỉ trong 1 trang).
    /// </summary>
    Task<List<SalesOrder>> GetPagedListAsync(
        string? keyword, int skip, int take, int? statusId = null, string? channel = null);

    Task<int> CountAsync(string? keyword, int? statusId = null, string? channel = null);
}
