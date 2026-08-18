using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IOutboundOrderRepository : IRepositoryBase<OutboundOrder, int>
{
    /// <summary>
    /// Load OutboundOrder với đầy đủ: Items → Allocations → Inventory, PaddyLot, Location.
    /// Dùng cho ConfirmDispatch và các bước cần đọc lot thực xuất.
    /// </summary>
    Task<OutboundOrder?> GetByIdDetailAsync(int id);

        /// <summary>
    /// Trang danh sách phiếu xuất. [outboundStatusId] lọc ngay trên DB để số
    /// trang luôn khớp với bộ lọc (tránh lọc client-side chỉ trong 1 trang).
    /// </summary>
    Task<List<OutboundOrder>> GetPagedListAsync(
        string? keyword, int skip, int take, int? outboundStatusId = null);

    Task<int> CountAsync(string? keyword, int? outboundStatusId = null);

}
