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

        var baseQuery = _context.Inventories.Where(x => !x.IsDeleted);

        // P0-3: đếm tổng (chưa lọc) trên bảng gốc, không qua projection nặng (nhiều join + CASE).
        var totalRecord = await baseQuery.CountAsync();

        // P1-1: đưa tìm kiếm keyword ra TRƯỚC projection để MySQL lọc trước rồi mới tính projection
        // (ghép LocationCode, join Product...) cho các dòng khớp. Điều kiện giữ NGUYÊN như cũ,
        // chỉ tham chiếu qua navigation thay vì alias của projection.
        if (!string.IsNullOrEmpty(keyword))
        {
            baseQuery = baseQuery.Where(x =>
                EF.Functions.Collate(x.ProductVariant.SKU, SQLParams.Latin_General).Contains(keyword) ||
                EF.Functions.Collate(x.ProductVariant.Name, SQLParams.Latin_General).Contains(keyword) ||
                EF.Functions.Collate(x.ProductVariant.Product.Name, SQLParams.Latin_General).Contains(keyword) ||
                EF.Functions.Collate(x.Warehouse.Name, SQLParams.Latin_General).Contains(keyword) ||
                (x.PaddyLot != null && x.PaddyLot.LotCode != null && EF.Functions.Collate(x.PaddyLot.LotCode, SQLParams.Latin_General).Contains(keyword)) ||
                (x.Location != null && EF.Functions.Collate(
                    (string.IsNullOrEmpty(x.Location.ZoneName) ? "" : x.Location.ZoneName)
                        + (string.IsNullOrEmpty(x.Location.ShelfRow) ? "" : "-" + x.Location.ShelfRow)
                        + (string.IsNullOrEmpty(x.Location.ShelfLevel) ? "" : "-" + x.Location.ShelfLevel)
                        + (string.IsNullOrEmpty(x.Location.SlotCode) ? "" : "-" + x.Location.SlotCode),
                    SQLParams.Latin_General).Contains(keyword)));
        }

        var query = baseQuery
            .Select(x => new InventoryAggregate
            {
                Id = x.Id,
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
                SKU = x.ProductVariant.SKU,
                ProductVariantName = x.ProductVariant.Name,
                ProductId = x.ProductVariant.ProductId,
                ProductName = x.ProductVariant.Product.Name,
                ProductCategoryId = x.ProductVariant.Product.ProductCategoryId,
                CategoryName = x.ProductVariant.Product.ProductCategory.Name,
                IsByproduct = x.ProductVariant.IsByproduct,
                UnitName = x.ProductVariant.UnitOfMeasure.Name,
                UnitWeightKg = x.ProductVariant.Weight,
                CostPrice = x.CostPrice,
                QuantityOnHand = x.QuantityOnHand,
                QuantityReserved = x.QuantityReserved,
                QuantityAvailable = x.QuantityOnHand - x.QuantityReserved,
                QuantityQuarantine =
                    ((x.PaddyLot != null && x.PaddyLot.Status.Code == LotStatusCodeConstants.Quarantine)
                        || (x.Location != null && x.Location.IsQuarantine))
                        ? x.QuantityOnHand : 0m,
                QuarantinedKg =
                    ((x.PaddyLot != null && x.PaddyLot.Status.Code == LotStatusCodeConstants.Quarantine)
                        || (x.Location != null && x.Location.IsQuarantine))
                        ? x.QuantityOnHand : 0m,
                SellableOnHandKg =
                    (!((x.PaddyLot != null && x.PaddyLot.Status.Code == LotStatusCodeConstants.Quarantine)
                        || (x.Location != null && x.Location.IsQuarantine))
                     && (x.PaddyLotId == null || x.PaddyLot.Status.IsSellable))
                        ? x.QuantityOnHand : 0m,
                OtherBlockedKg =
                    (!((x.PaddyLot != null && x.PaddyLot.Status.Code == LotStatusCodeConstants.Quarantine)
                        || (x.Location != null && x.Location.IsQuarantine))
                     && x.PaddyLot != null && !x.PaddyLot.Status.IsSellable)
                        ? x.QuantityOnHand : 0m,
                QuantityProcessing =
                    (!((x.PaddyLot != null && x.PaddyLot.Status.Code == LotStatusCodeConstants.Quarantine)
                        || (x.Location != null && x.Location.IsQuarantine))
                        && x.PaddyLot != null
                        && (x.PaddyLot.Status.Code == LotStatusCodeConstants.Processing
                            || x.PaddyLot.Status.Code == LotStatusCodeConstants.Milling))
                        ? x.QuantityOnHand : 0m,
                // QuantityOnHand lưu kg trực tiếp. Số bao phải lấy từ bao vật lý, tuyệt đối
                // không suy ngược bằng kg / ProductVariant.Weight vì Weight không phải lúc nào
                // cũng là quy cách bao và phép chia đó làm sai dữ liệu lịch sử.
                TotalWeightKg = x.QuantityOnHand,
                // Bao hỗn hợp có thể mang LotId đại diện khác với lô của dòng tồn.
                // Xác định dữ liệu vật lý theo thành phần bao, nhưng chỉ đếm bao trên
                // dòng lô đại diện để tổng số bao tại vị trí không bị nhân đôi.
                HasPhysicalBagData = x.PaddyLotId.HasValue && _context.PaddyLotBagContents.Any(c =>
                    !c.IsDeleted && c.WeightKg > 0 && c.LotId == x.PaddyLotId.Value &&
                    !c.Bag.IsDeleted && c.Bag.LocationId == x.LocationId &&
                    c.Bag.Status == "Stored" && c.Bag.WeightKg > 0),
                Bags = x.PaddyLotId.HasValue
                    ? _context.PaddyLotBags.Count(b =>
                        !b.IsDeleted && b.LotId == x.PaddyLotId.Value && b.LocationId == x.LocationId &&
                        b.Status == "Stored" && b.WeightKg > 0)
                    : 0,
                OpenBags = x.PaddyLotId.HasValue
                    ? _context.PaddyLotBags.Count(b =>
                        !b.IsDeleted && b.LotId == x.PaddyLotId.Value && b.LocationId == x.LocationId &&
                        b.Status == "Stored" && b.WeightKg > 0 && !b.IsFull)
                    : 0,
                MinStockLevel = x.ProductVariant.MinStockLevel,
                IsLowStock = x.ProductVariant.MinStockLevel != null &&
                             x.QuantityOnHand <= x.ProductVariant.MinStockLevel,
                PaddyLotId = x.PaddyLotId,
                LotCode = x.PaddyLot != null ? x.PaddyLot.LotCode : null,
                LotType = x.PaddyLot != null ? x.PaddyLot.LotType : null,
                LotInboundDate = x.PaddyLot != null ? x.PaddyLot.InboundDate : (DateTime?)null,
                LotQualityStatus = x.PaddyLot != null ? x.PaddyLot.QualityStatus : null,
                LotCostPricePerKg = x.PaddyLot != null ? x.PaddyLot.CostPricePerKg : (decimal?)null,
                LotStatusId = x.PaddyLot != null ? x.PaddyLot.StatusId : (int?)null,
                LotStatusName = x.PaddyLot != null ? x.PaddyLot.Status.Name : null,
                LotStatusColor = x.PaddyLot != null ? x.PaddyLot.Status.Color : null,
                LotIsSellable = x.PaddyLot != null ? x.PaddyLot.Status.IsSellable : (bool?)null,
                LastStockTakeDate = x.LastStockTakeDate,
                CreatedDate = x.CreatedDate
            });

        // (totalRecord + tìm kiếm keyword đã được xử lý trên baseQuery phía trên, trước projection.)

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

        if (parameters.ProductCategoryId.HasValue)
        {
            query = query.Where(x => x.ProductCategoryId == parameters.ProductCategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.LotType))
        {
            query = query.Where(x => x.LotType == parameters.LotType);
        }

        if (parameters.LotStatusId.HasValue)
        {
            query = query.Where(x => x.LotStatusId == parameters.LotStatusId.Value);
        }

        if (parameters.WithLotOnly == true)
        {
            query = query.Where(x => x.PaddyLotId != null);
        }

        if (parameters.IsQuarantined == true || (parameters.InventoryState != null && parameters.InventoryState.Equals("QUARANTINED", StringComparison.OrdinalIgnoreCase)))
        {
            query = query.Where(x => x.QuarantinedKg > 0);
        }
        else if (parameters.IsQuarantined == false)
        {
            query = query.Where(x => x.QuarantinedKg == 0);
        }

        if (parameters.InventoryState != null)
        {
            if (parameters.InventoryState.Equals("SELLABLE", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.SellableOnHandKg > 0);
            }
            else if (parameters.InventoryState.Equals("OTHER_BLOCKED", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.OtherBlockedKg > 0);
            }
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

    /// <summary>
    /// Tổng hợp tồn kho theo trạng thái (Tồn thực tế / Khả dụng / Đã giữ / Đang xử lý / Cách ly)
    /// cho 5 thẻ KPI. Phân bổ tồn thực tế thành 4 nhóm không chồng lấn:
    /// Cách ly → Đang xử lý → Đã giữ → Khả dụng (phần còn lại).
    /// </summary>
    public async Task<InventoryStockSummaryAggregate> GetStockSummaryAsync(InventorySummaryParameters parameters)
    {
        var query = _context.Inventories.Where(x => !x.IsDeleted);

        if (parameters.WarehouseId.HasValue)
        {
            query = query.Where(x => x.WarehouseId == parameters.WarehouseId.Value);
        }

        if (parameters.LocationId.HasValue)
        {
            query = query.Where(x => x.LocationId == parameters.LocationId.Value);
        }

        if (parameters.ProductCategoryId.HasValue)
        {
            query = query.Where(x => x.ProductVariant.Product.ProductCategoryId == parameters.ProductCategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.LotType))
        {
            query = query.Where(x => x.PaddyLot != null && x.PaddyLot.LotType == parameters.LotType);
        }

        if (parameters.ProductVariantId.HasValue)
        {
            query = query.Where(x => x.ProductVariantId == parameters.ProductVariantId.Value);
        }

        if (parameters.LotStatusId.HasValue)
        {
            query = query.Where(x => x.PaddyLot != null && x.PaddyLot.StatusId == parameters.LotStatusId.Value);
        }

        if (parameters.WithLotOnly == true)
        {
            query = query.Where(x => x.PaddyLotId != null);
        }

        // Các điều kiện dưới đây phải khớp TỪNG CHỮ với GetPagedAsync, nếu không
        // 5 thẻ và bảng sẽ nói hai chuyện khác nhau trên cùng một bộ lọc.
        // Bảng lọc trên projection (QuarantinedKg > 0 …), ở đây viết lại theo
        // điều kiện gốc sinh ra chính projection đó.
        var quarantineState = parameters.IsQuarantined
            ?? (parameters.InventoryState != null
                && parameters.InventoryState.Equals("QUARANTINED", StringComparison.OrdinalIgnoreCase)
                ? true
                : (bool?)null);

        if (quarantineState == true)
        {
            query = query.Where(x =>
                ((x.PaddyLot != null && x.PaddyLot.Status.Code == LotStatusCodeConstants.Quarantine)
                    || (x.Location != null && x.Location.IsQuarantine))
                && x.QuantityOnHand > 0);
        }
        else if (quarantineState == false)
        {
            // Tương đương "QuarantinedKg == 0" của bảng: không cách ly HOẶC
            // dòng cách ly nhưng đã hết hàng.
            query = query.Where(x =>
                !((x.PaddyLot != null && x.PaddyLot.Status.Code == LotStatusCodeConstants.Quarantine)
                    || (x.Location != null && x.Location.IsQuarantine))
                || x.QuantityOnHand <= 0);
        }

        if (parameters.InventoryState != null)
        {
            if (parameters.InventoryState.Equals("SELLABLE", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x =>
                    !((x.PaddyLot != null && x.PaddyLot.Status.Code == LotStatusCodeConstants.Quarantine)
                        || (x.Location != null && x.Location.IsQuarantine))
                    && (x.PaddyLotId == null || x.PaddyLot.Status.IsSellable)
                    && x.QuantityOnHand > 0);
            }
            else if (parameters.InventoryState.Equals("OTHER_BLOCKED", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x =>
                    !((x.PaddyLot != null && x.PaddyLot.Status.Code == LotStatusCodeConstants.Quarantine)
                        || (x.Location != null && x.Location.IsQuarantine))
                    && x.PaddyLot != null && !x.PaddyLot.Status.IsSellable
                    && x.QuantityOnHand > 0);
            }
        }

        if (parameters.LowStockOnly == true)
        {
            query = query.Where(x =>
                x.ProductVariant.MinStockLevel != null
                && x.QuantityOnHand <= x.ProductVariant.MinStockLevel);
        }

        var summaryKeyword = parameters.Search?.Trim();
        if (!string.IsNullOrEmpty(summaryKeyword))
        {
            query = query.Where(x =>
                EF.Functions.Collate(x.ProductVariant.SKU, SQLParams.Latin_General).Contains(summaryKeyword) ||
                EF.Functions.Collate(x.ProductVariant.Name, SQLParams.Latin_General).Contains(summaryKeyword) ||
                EF.Functions.Collate(x.ProductVariant.Product.Name, SQLParams.Latin_General).Contains(summaryKeyword) ||
                EF.Functions.Collate(x.Warehouse.Name, SQLParams.Latin_General).Contains(summaryKeyword) ||
                (x.PaddyLot != null && x.PaddyLot.LotCode != null && EF.Functions.Collate(x.PaddyLot.LotCode, SQLParams.Latin_General).Contains(summaryKeyword)) ||
                (x.Location != null && EF.Functions.Collate(
                    (string.IsNullOrEmpty(x.Location.ZoneName) ? "" : x.Location.ZoneName)
                        + (string.IsNullOrEmpty(x.Location.ShelfRow) ? "" : "-" + x.Location.ShelfRow)
                        + (string.IsNullOrEmpty(x.Location.ShelfLevel) ? "" : "-" + x.Location.ShelfLevel)
                        + (string.IsNullOrEmpty(x.Location.SlotCode) ? "" : "-" + x.Location.SlotCode),
                    SQLParams.Latin_General).Contains(summaryKeyword)));
        }

        // Nhóm CÁCH LY (CL): lô "Cách ly" hoặc vị trí cách ly.
        var quarantineQuery = query.Where(x =>
            (x.PaddyLot != null && x.PaddyLot.Status.Code == LotStatusCodeConstants.Quarantine)
            || (x.Location != null && x.Location.IsQuarantine));

        // Nhóm ĐANG XỬ LÝ (XL): lô "Chờ xử lý"/"Đang xay" và KHÔNG thuộc nhóm cách ly.
        var processingQuery = query.Where(x =>
            !((x.PaddyLot != null && x.PaddyLot.Status.Code == LotStatusCodeConstants.Quarantine)
                || (x.Location != null && x.Location.IsQuarantine))
            && x.PaddyLot != null
            && (x.PaddyLot.Status.Code == LotStatusCodeConstants.Processing
                || x.PaddyLot.Status.Code == LotStatusCodeConstants.Milling));

        // Phần còn lại (không cách ly, không đang xử lý) — nơi tính "Đã giữ".
        var normalQuery = query.Where(x =>
            !((x.PaddyLot != null && x.PaddyLot.Status.Code == LotStatusCodeConstants.Quarantine)
                || (x.Location != null && x.Location.IsQuarantine))
            && !(x.PaddyLot != null
                && (x.PaddyLot.Status.Code == LotStatusCodeConstants.Processing
                    || x.PaddyLot.Status.Code == LotStatusCodeConstants.Milling)));

        var dto = new InventoryStockSummaryAggregate
        {
            // *WeightKg: tổng kg thực tế (QuantityOnHand lưu kg trực tiếp)
            TotalOnHandWeightKg = await query.SumAsync(x => (decimal?)x.QuantityOnHand) ?? 0m,
            TotalQuarantineWeightKg = await quarantineQuery.SumAsync(x => (decimal?)x.QuantityOnHand) ?? 0m,
            TotalProcessingWeightKg = await processingQuery.SumAsync(x => (decimal?)x.QuantityOnHand) ?? 0m,
            TotalReservedWeightKg = await normalQuery.SumAsync(x => (decimal?)x.QuantityReserved) ?? 0m,

            // Total* (số bao): Floor(QoH / Weight) từng dòng rồi cộng.
            // Không dùng Math.Floor bên trong SumAsync vì EF Core / Pomelo không dịch được
            // Math.Floor sang FLOOR() trong biểu thức tổng hợp → client-evaluation.
            // Thay bằng ToListAsync chỉ chiếu 2 cột rồi tính Sum ở application layer.
            TotalOnHand = (decimal)(await query
                .Where(x => x.ProductVariant.Weight > 0)
                .Select(x => new { x.QuantityOnHand, x.ProductVariant.Weight })
                .ToListAsync())
                .Sum(x => Math.Floor((double)x.QuantityOnHand / (double)x.Weight)),
            TotalQuarantine = (decimal)(await quarantineQuery
                .Where(x => x.ProductVariant.Weight > 0)
                .Select(x => new { x.QuantityOnHand, x.ProductVariant.Weight })
                .ToListAsync())
                .Sum(x => Math.Floor((double)x.QuantityOnHand / (double)x.Weight)),
            TotalProcessing = (decimal)(await processingQuery
                .Where(x => x.ProductVariant.Weight > 0)
                .Select(x => new { x.QuantityOnHand, x.ProductVariant.Weight })
                .ToListAsync())
                .Sum(x => Math.Floor((double)x.QuantityOnHand / (double)x.Weight)),
            TotalReserved = (decimal)(await normalQuery
                .Where(x => x.ProductVariant.Weight > 0)
                .Select(x => new { x.QuantityReserved, x.ProductVariant.Weight })
                .ToListAsync())
                .Sum(x => Math.Floor((double)x.QuantityReserved / (double)x.Weight)),

            LineCount = await query.CountAsync(),
            LowStockCount = await query.CountAsync(x =>
                x.ProductVariant.MinStockLevel != null && x.QuantityOnHand <= x.ProductVariant.MinStockLevel),
            QuarantineLotCount = await quarantineQuery
                .Where(x => x.PaddyLotId != null)
                .Select(x => x.PaddyLotId)
                .Distinct()
                .CountAsync()
        };

        // Khả dụng = Tồn thực tế − Cách ly − Đang xử lý − Đã giữ (phân bổ không chồng lấn).
        dto.TotalAvailable = dto.TotalOnHand - dto.TotalQuarantine - dto.TotalProcessing - dto.TotalReserved;
        dto.TotalAvailableWeightKg = dto.TotalOnHandWeightKg - dto.TotalQuarantineWeightKg
            - dto.TotalProcessingWeightKg - dto.TotalReservedWeightKg;

        return dto;
    }

    public async Task<Inventory?> GetByIdDetailAsync(int id)
    {
        return await _context.Inventories
            .AsNoTracking() // chỉ đọc để hiển thị chi tiết
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
    public async Task<Inventory?> GetByVariantWarehouseLocationAsync(int productVariantId, int warehouseId, int? locationId, int? paddyLotId = null)
    {
        return await _context.Inventories
            .FirstOrDefaultAsync(x =>
                !x.IsDeleted &&
                x.ProductVariantId == productVariantId &&
                x.WarehouseId == warehouseId &&
                x.LocationId == locationId &&
                x.PaddyLotId == paddyLotId);
    }

    /// <summary>
    /// Lấy dữ liệu theo điều kiện nghiệp vụ cụ thể thay vì chỉ theo id.
    /// </summary>
    /// <param name="productVariantId">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public async Task<List<Inventory>> GetByProductVariantAsync(int productVariantId)
    {
        return await _context.Inventories
            .AsNoTracking() // chỉ đọc để hiển thị
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
            .AsNoTracking() // chỉ đọc để hiển thị / cảnh báo tồn thấp
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

    /// <summary>
    /// Tồn khả dụng để bán: theo từng lô (PaddyLotId), tại kho chỉ định.
    /// Điều kiện: lot phải có IsSellable=true, không bị cách ly, AvailableQty > 0.
    /// Kết quả sắp xếp FIFO: nhập sớm nhất → bán trước.
    /// </summary>
    public async Task<List<Inventory>> GetAvailableForSalesAsync(int productVariantId, int warehouseId)
    {
        return await _context.Inventories
            .Include(x => x.PaddyLot)
                .ThenInclude(pl => pl!.Status)
            .Include(x => x.Location)
            .Where(x =>
                !x.IsDeleted &&
                x.ProductVariantId == productVariantId &&
                x.WarehouseId == warehouseId &&
                // Tồn khả dụng phải dương
                (x.QuantityOnHand - x.QuantityReserved) > 0 &&
                // Không bị cách ly (cả Location cách ly và Lot cách ly)
                !(x.Location != null && x.Location.IsQuarantine) &&
                (x.PaddyLotId == null || (x.PaddyLot != null && x.PaddyLot.Status.Code != LotStatusCodeConstants.Quarantine)) &&
                // Nếu có lô: lô phải IsSellable
                (x.PaddyLotId == null || (x.PaddyLot != null && x.PaddyLot.Status.IsSellable)))
            // FIFO: lot nhập sớm hơn được chọn trước
            .OrderBy(x => x.PaddyLot != null ? x.PaddyLot.InboundDate : x.CreatedDate)
            .ThenBy(x => x.Id)
            .ToListAsync();
    }
}
