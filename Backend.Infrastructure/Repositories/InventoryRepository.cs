using System;
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

public class InventoryRepository : RepositoryBase<Inventory, int>, IInventoryRepository
{
    private readonly BackendContext _context;

    public InventoryRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<InventoryAggregate>> GetPagedAsync(InventoryDTParameters parameters)
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
            orderCriteria = "Id";
            orderAscendingDirection = false;
        }

        var query = _context.Inventories
            .Where(x => !x.IsDeleted)
            .Select(x => new InventoryAggregate
            {
                Id = x.Id,
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
                SKU = x.ProductVariant.SKU,
                ProductVariantName = x.ProductVariant.Name,
                ProductId = x.ProductVariant.ProductId,
                ProductName = x.ProductVariant.Product.Name,
                PurchaseOrderId = x.PurchaseOrderId,
                CostPrice = x.CostPrice,
                QuantityOnHand = x.QuantityOnHand,
                QuantityReserved = x.QuantityReserved,
                QuantityAvailable = x.QuantityOnHand - x.QuantityReserved,
                MinStockLevel = x.ProductVariant.MinStockLevel,
                IsLowStock = x.ProductVariant.MinStockLevel != null &&
                             x.QuantityOnHand <= x.ProductVariant.MinStockLevel,
                LastStockTakeDate = x.LastStockTakeDate,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(x =>
                EF.Functions.Collate(x.SKU, SQLParams.Latin_General).Contains(keyword) ||
                EF.Functions.Collate(x.ProductVariantName, SQLParams.Latin_General).Contains(keyword) ||
                EF.Functions.Collate(x.ProductName, SQLParams.Latin_General).Contains(keyword) ||
                EF.Functions.Collate(x.WarehouseName, SQLParams.Latin_General).Contains(keyword) ||
                (x.LocationCode != null && EF.Functions.Collate(x.LocationCode, SQLParams.Latin_General).Contains(keyword)));
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

        if (parameters.LowStockOnly == true)
        {
            query = query.Where(x => x.IsLowStock);
        }

        query = orderAscendingDirection
            ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc)
            : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = new DTResult<InventoryAggregate>
        {
            draw = parameters.Draw,
            data = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync(),
            recordsFiltered = await query.CountAsync(),
            recordsTotal = totalRecord
        };

        return data;
    }

    public async Task<Inventory?> GetByIdDetailAsync(int id)
    {
        return await _context.Inventories
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
    /// <param name="warehouseId">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <param name="locationId">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public async Task<Inventory?> GetByVariantWarehouseLocationAsync(int productVariantId, int warehouseId, int? locationId)
    {
        return await _context.Inventories
            .FirstOrDefaultAsync(x =>
                !x.IsDeleted &&
                x.ProductVariantId == productVariantId &&
                x.WarehouseId == warehouseId &&
                x.LocationId == locationId);
    }

    /// <summary>
    /// Lấy dữ liệu theo điều kiện nghiệp vụ cụ thể thay vì chỉ theo id.
    /// </summary>
    /// <param name="productVariantId">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public async Task<List<Inventory>> GetByProductVariantAsync(int productVariantId)
    {
        return await _context.Inventories
            .Include(x => x.Warehouse)
            .Include(x => x.Location)
            .Include(x => x.ProductVariant)
                .ThenInclude(x => x.Product)
            .Where(x => !x.IsDeleted && x.ProductVariantId == productVariantId)
            .OrderBy(x => x.WarehouseId)
            .ThenBy(x => x.LocationId)
            .ToListAsync();
    }

    /// <summary>
    /// Xử lý nghiệp vụ tồn kho, bao gồm nhập/xuất/điều chỉnh tồn và ghi nhận lịch sử InventoryTransaction.
    /// </summary>
    /// <param name="warehouseId">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <param name="limit">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public async Task<List<Inventory>> GetLowStockAsync(int? warehouseId, int limit = 50)
    {
        var query = _context.Inventories
            .Include(x => x.Warehouse)
            .Include(x => x.Location)
            .Include(x => x.ProductVariant)
                .ThenInclude(x => x.Product)
            .Where(x =>
                !x.IsDeleted &&
                x.ProductVariant.MinStockLevel != null &&
                x.QuantityOnHand <= x.ProductVariant.MinStockLevel);

        if (warehouseId.HasValue)
        {
            query = query.Where(x => x.WarehouseId == warehouseId.Value);
        }

        return await query
            .OrderBy(x => x.QuantityOnHand)
            .ThenBy(x => x.ProductVariantId)
            .Take(limit)
            .ToListAsync();
    }
}
