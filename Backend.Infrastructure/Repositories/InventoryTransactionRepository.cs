using System;
using Backend.Application.Constants;
using Backend.Domain.Abstractions;
using Backend.Domain.Aggregates;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Backend.Share.Constants;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Repositories;

public class InventoryTransactionRepository : RepositoryBase<InventoryTransaction, int>, IInventoryTransactionRepository
{
    private readonly BackendContext _context;

    public InventoryTransactionRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<InventoryTransactionAggregate>> GetPagedAsync(InventoryTransactionDTParameters parameters)
    {
        var keyword = parameters.Search?.Value;
        var orderCriteria = string.Empty;
        var orderAscendingDirection = true;

        if (parameters.Order != null && parameters.Order.Any())
        {
            orderCriteria = parameters.Columns[parameters.Order[0].Column].Data;
            orderAscendingDirection = parameters.Order[0].Dir.ToString().ToLower() == "asc";
        }
        else
        {
            orderCriteria = "CreatedDate";
            orderAscendingDirection = false;
        }

        // Bảng gốc (chưa projection). Đếm tổng và áp bộ lọc ở đây để tránh phải tính
        // các cột projection (ghép LocationCode, join Product...) cho mọi dòng chỉ để đếm/lọc.
        var baseQuery = _context.InventoryTransactions.Where(x => !x.IsDeleted);

        var totalRecord = await baseQuery.CountAsync();

        // Bộ lọc giữ NGUYÊN điều kiện như cũ, chỉ tham chiếu qua navigation thay vì alias projection.
        if (!string.IsNullOrEmpty(keyword))
        {
            baseQuery = baseQuery.Where(x =>
                (x.ProductVariant != null && x.ProductVariant.SKU != null && EF.Functions.Collate(x.ProductVariant.SKU, SQLParams.Latin_General).Contains(keyword)) ||
                (x.ProductVariant != null && x.ProductVariant.Name != null && EF.Functions.Collate(x.ProductVariant.Name, SQLParams.Latin_General).Contains(keyword)) ||
                (x.ProductVariant != null && x.ProductVariant.Product.Name != null && EF.Functions.Collate(x.ProductVariant.Product.Name, SQLParams.Latin_General).Contains(keyword)) ||
                EF.Functions.Collate(x.TransactionType, SQLParams.Latin_General).Contains(keyword) ||
                (x.ReferenceType != null && EF.Functions.Collate(x.ReferenceType, SQLParams.Latin_General).Contains(keyword)) ||
                (x.Note != null && EF.Functions.Collate(x.Note, SQLParams.Latin_General).Contains(keyword)));
        }

        if (parameters.WarehouseId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.WarehouseId == parameters.WarehouseId.Value);
        }

        if (parameters.LocationId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.LocationId == parameters.LocationId.Value);
        }

        if (parameters.ProductVariantId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.ProductVariantId == parameters.ProductVariantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.TransactionType))
        {
            var type = InventoryTransactionTypeConstants.Normalize(parameters.TransactionType);
            baseQuery = baseQuery.Where(x => x.TransactionType == type);
        }

        if (!string.IsNullOrWhiteSpace(parameters.ReferenceType))
        {
            var referenceType = InventoryReferenceTypeConstants.Normalize(parameters.ReferenceType);
            baseQuery = baseQuery.Where(x => x.ReferenceType == referenceType);
        }

        if (parameters.ReferenceId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.ReferenceId == parameters.ReferenceId.Value);
        }

        if (parameters.FromDate.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.CreatedDate >= parameters.FromDate.Value);
        }

        if (parameters.ToDate.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.CreatedDate <= parameters.ToDate.Value);
        }

        // Đếm sau lọc trên bảng gốc — bằng đúng số dòng của projection (quan hệ 1-1).
        var recordsFiltered = await baseQuery.CountAsync();

        var query = baseQuery
            .Select(x => new InventoryTransactionAggregate
            {
                Id = x.Id,
                InventoryId = x.InventoryId,
                WarehouseId = x.WarehouseId,
                WarehouseName = x.Warehouse.Name,
                LocationId = x.LocationId,
                // Ghép mã vị trí bằng chuỗi nối (translatable sang SQL). Không dùng string.Join
                // vì EF Core không dịch được string.Join trong projection -> lỗi 500 khi query.
                LocationCode = x.Location == null
                    ? null
                    : (string.IsNullOrEmpty(x.Location.ZoneName) ? "" : x.Location.ZoneName)
                        + (string.IsNullOrEmpty(x.Location.ShelfRow) ? "" : "-" + x.Location.ShelfRow)
                        + (string.IsNullOrEmpty(x.Location.ShelfLevel) ? "" : "-" + x.Location.ShelfLevel)
                        + (string.IsNullOrEmpty(x.Location.SlotCode) ? "" : "-" + x.Location.SlotCode),
                ProductVariantId = x.ProductVariantId,
                SKU = x.ProductVariant != null ? x.ProductVariant.SKU : null,
                ProductVariantName = x.ProductVariant != null ? x.ProductVariant.Name : null,
                ProductName = x.ProductVariant != null ? x.ProductVariant.Product.Name : null,
                TransactionType = x.TransactionType,
                ReferenceType = x.ReferenceType,
                ReferenceId = x.ReferenceId,
                ReferenceItemId = x.ReferenceItemId,
                Quantity = x.Quantity,
                BeforeQuantity = x.BeforeQuantity,
                AfterQuantity = x.AfterQuantity,
                WeightKg = x.WeightKg,
                Note = x.Note,
                CreatedDate = x.CreatedDate,
                CreatedBy = x.CreatedBy
            });

        query = orderAscendingDirection
            ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc)
            : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = new DTResult<InventoryTransactionAggregate>
        {
            draw = parameters.Draw,
            data = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync(),
            recordsFiltered = recordsFiltered,
            recordsTotal = totalRecord
        };

        return data;
    }

    /// <summary>
    /// Lấy chi tiết một bản ghi theo id. Nếu không tìm thấy thì tầng service sẽ trả NotFound để API phản hồi 404.
    /// </summary>
    /// <param name="id">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public async Task<InventoryTransaction?> GetByIdDetailAsync(int id)
    {
        return await _context.InventoryTransactions
            .Include(x => x.Inventory)
            .Include(x => x.Warehouse)
            .Include(x => x.Location)
            .Include(x => x.ProductVariant)
                .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == id);
    }

    /// <summary>
    /// Lấy dữ liệu theo điều kiện nghiệp vụ cụ thể thay vì chỉ theo id.
    /// </summary>
    /// <param name="productVariantId">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <param name="limit">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public async Task<List<InventoryTransaction>> GetByProductVariantAsync(int productVariantId, int limit = 100)
    {
        return await _context.InventoryTransactions
            .Include(x => x.Inventory)
            .Include(x => x.Warehouse)
            .Include(x => x.Location)
            .Include(x => x.ProductVariant)
                .ThenInclude(x => x.Product)
            .Where(x => !x.IsDeleted && x.ProductVariantId == productVariantId)
            .OrderByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Ghi giao dịch tồn kho, quy đổi Before/After sang TỔNG TỒN CỦA CỘT (Location).
    /// Before/After truyền vào là tồn TRƯỚC/SAU của DÒNG lô; hàm cộng thêm tổng tồn các dòng
    /// KHÁC cùng cột (loại trừ chính dòng của giao dịch này) để ra tồn của cả cột.
    /// Không phụ thuộc việc dòng hiện tại đã được flush hay chưa vì luôn loại trừ theo InventoryId.
    /// LocationId = null (giao dịch không gắn vị trí) thì giữ nguyên tồn theo dòng.
    /// </summary>
    public async Task CreateWithColumnTotalsAsync(InventoryTransaction transaction)
    {
        // RESERVE / RELEASE_RESERVE theo dõi lượng GIỮ (QuantityReserved), không phải tồn on-hand
        // của cột → giữ nguyên Before/After, không quy đổi.
        var isReservation = transaction.TransactionType == InventoryTransactionTypeConstants.Reserve
            || transaction.TransactionType == InventoryTransactionTypeConstants.ReleaseReserve;

        if (transaction.LocationId.HasValue && !isReservation)
        {
            var locationId = transaction.LocationId.Value;
            var otherOnHand = await _context.Inventories
                .Where(i => i.LocationId == locationId
                            && !i.IsDeleted
                            && i.Id != transaction.InventoryId)
                .SumAsync(i => i.QuantityOnHand);

            transaction.BeforeQuantity += otherOnHand;
            transaction.AfterQuantity += otherOnHand;
        }

        await CreateAsync(transaction);
    }
}
