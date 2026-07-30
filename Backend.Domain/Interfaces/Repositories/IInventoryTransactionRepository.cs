using System;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IInventoryTransactionRepository : IRepositoryBase<InventoryTransaction, int>
{
    Task<DTResult<InventoryTransactionAggregate>> GetPagedAsync(InventoryTransactionDTParameters parameters);

    Task<InventoryTransaction?> GetByIdDetailAsync(int id);

    Task<List<InventoryTransaction>> GetByProductVariantAsync(int productVariantId, int limit = 100);

    /// <summary>
    /// Ghi giao dịch tồn kho nhưng quy đổi Before/After sang TỔNG TỒN CỦA CỘT (Location),
    /// thay vì tồn theo từng dòng lô. Trước khi gọi, <c>BeforeQuantity</c>/<c>AfterQuantity</c>
    /// phải đang chứa tồn TRƯỚC/SAU của dòng Inventory (variant + kho + vị trí + lô); hàm sẽ cộng
    /// thêm tổng tồn của các dòng KHÁC cùng cột để ra tồn của cả cột. LocationId = null → giữ nguyên.
    /// </summary>
    Task CreateWithColumnTotalsAsync(InventoryTransaction transaction);
}
