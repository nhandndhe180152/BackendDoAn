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

        var query = _context.InventoryTransactions
            .Where(x => !x.IsDeleted)
            .Select(x => new InventoryTransactionAggregate
            {
                Id = x.Id,
                InventoryId = x.InventoryId,
                WarehouseId = x.WarehouseId,
                WarehouseName = x.Warehouse.Name,
                LocationId = x.LocationId,
                LocationCode = x.Location == null
                    ? null
                    : string.Join("-",
                        new[]
                        {
                            x.Location.ZoneName,
                            x.Location.ShelfRow,
                            x.Location.ShelfLevel,
                            x.Location.SlotCode
                        }.Where(s => !string.IsNullOrWhiteSpace(s))),
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
                IotWeightLogId = x.IotWeightLogId,
                Note = x.Note,
                CreatedDate = x.CreatedDate,
                CreatedBy = x.CreatedBy
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(x =>
                (x.SKU != null && EF.Functions.Collate(x.SKU, SQLParams.Latin_General).Contains(keyword)) ||
                (x.ProductVariantName != null && EF.Functions.Collate(x.ProductVariantName, SQLParams.Latin_General).Contains(keyword)) ||
                (x.ProductName != null && EF.Functions.Collate(x.ProductName, SQLParams.Latin_General).Contains(keyword)) ||
                EF.Functions.Collate(x.TransactionType, SQLParams.Latin_General).Contains(keyword) ||
                (x.ReferenceType != null && EF.Functions.Collate(x.ReferenceType, SQLParams.Latin_General).Contains(keyword)) ||
                (x.Note != null && EF.Functions.Collate(x.Note, SQLParams.Latin_General).Contains(keyword)));
        }

        if (parameters.WarehouseId.HasValue)
        {
            query = query.Where(x => x.WarehouseId == parameters.WarehouseId.Value);
        }

        if (parameters.LocationId.HasValue)
        {
            query = query.Where(x => x.LocationId == parameters.LocationId.Value);
        }

        if (parameters.ProductVariantId.HasValue)
        {
            query = query.Where(x => x.ProductVariantId == parameters.ProductVariantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.TransactionType))
        {
            var type = InventoryTransactionTypeConstants.Normalize(parameters.TransactionType);
            query = query.Where(x => x.TransactionType == type);
        }

        if (!string.IsNullOrWhiteSpace(parameters.ReferenceType))
        {
            var referenceType = InventoryReferenceTypeConstants.Normalize(parameters.ReferenceType);
            query = query.Where(x => x.ReferenceType == referenceType);
        }

        if (parameters.ReferenceId.HasValue)
        {
            query = query.Where(x => x.ReferenceId == parameters.ReferenceId.Value);
        }

        if (parameters.FromDate.HasValue)
        {
            query = query.Where(x => x.CreatedDate >= parameters.FromDate.Value);
        }

        if (parameters.ToDate.HasValue)
        {
            query = query.Where(x => x.CreatedDate <= parameters.ToDate.Value);
        }

        query = orderAscendingDirection
            ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc)
            : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = new DTResult<InventoryTransactionAggregate>
        {
            draw = parameters.Draw,
            data = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync(),
            recordsFiltered = await query.CountAsync(),
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
            .Include(x => x.IotWeightLog)
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
}
