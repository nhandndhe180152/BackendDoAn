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

    Task<List<OutboundOrder>> GetPagedListAsync(string? keyword, int skip, int take);
    Task<int> CountAsync(string? keyword);
}
