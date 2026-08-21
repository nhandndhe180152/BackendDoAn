using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Common;
using Backend.Application.Constants;
using Backend.Application.DTOs.StockTakes;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Backend.Share.Extensions;

namespace Backend.Application.Implements;

public partial class StockTakeService : IStockTakeService
{
    private readonly IStockTakeRepository _stockTakeRepository;
    private readonly IStockTakeItemRepository _stockTakeItemRepository;
    private readonly IInventoryTransactionService _inventoryTransactionService;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ISystemConfigRepository _systemConfigRepository;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly IApplicationDbContext _context;

    public StockTakeService(
        IStockTakeRepository stockTakeRepository,
        IStockTakeItemRepository stockTakeItemRepository,
        IInventoryTransactionService inventoryTransactionService,
        IInventoryRepository inventoryRepository,
        ISystemConfigRepository systemConfigRepository,
        Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
        INotificationDispatcher notificationDispatcher,
        IApplicationDbContext context)
    {
        _stockTakeRepository = stockTakeRepository;
        _stockTakeItemRepository = stockTakeItemRepository;
        _inventoryTransactionService = inventoryTransactionService;
        _inventoryRepository = inventoryRepository;
        _systemConfigRepository = systemConfigRepository;
        _httpContextAccessor = httpContextAccessor;
        _notificationDispatcher = notificationDispatcher;
        _context = context;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Đọc ngưỡng phân loại chênh lệch từ SystemConfig (% và kg).
    /// Fallback về Defaults nếu chưa cấu hình hoặc giá trị không hợp lệ.
    /// </summary>
    private async Task<(decimal smallPct, decimal mediumPct, decimal smallKg, decimal mediumKg)> ResolveVarianceThresholdsAsync()
    {
        decimal smallPct  = SystemConfigConstants.Defaults.StockTakeSmallVariancePercent;
        decimal mediumPct = SystemConfigConstants.Defaults.StockTakeMediumVariancePercent;
        decimal smallKg   = SystemConfigConstants.Defaults.StockTakeSmallVarianceKg;
        decimal mediumKg  = SystemConfigConstants.Defaults.StockTakeMediumVarianceKg;

        // Dùng InvariantCulture để parse đúng dù server chạy locale bất kỳ.
        var smallPctRaw = await _systemConfigRepository.GetValueByKey(SystemConfigConstants.Keys.StockTakeSmallVariancePercent);
        if (decimal.TryParse(smallPctRaw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var pSmall) && pSmall > 0)
            smallPct = pSmall;

        var mediumPctRaw = await _systemConfigRepository.GetValueByKey(SystemConfigConstants.Keys.StockTakeMediumVariancePercent);
        if (decimal.TryParse(mediumPctRaw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var pMedium) && pMedium > smallPct)
            mediumPct = pMedium;

        var smallKgRaw = await _systemConfigRepository.GetValueByKey(SystemConfigConstants.Keys.StockTakeSmallVarianceKg);
        if (decimal.TryParse(smallKgRaw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var kSmall) && kSmall > 0)
            smallKg = kSmall;

        var mediumKgRaw = await _systemConfigRepository.GetValueByKey(SystemConfigConstants.Keys.StockTakeMediumVarianceKg);
        if (decimal.TryParse(mediumKgRaw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var kMedium) && kMedium > smallKg)
            mediumKg = kMedium;

        return (smallPct, mediumPct, smallKg, mediumKg);
    }

    /// <summary>
    /// Phân loại mức độ chênh lệch dựa trên CẢ % lẫn kg (FDS: lấy mức cao hơn giữa hai tiêu chí).
    /// </summary>
    /// <param name="variancePercent">null = chưa nhập hoặc SystemQty=0.</param>
    /// <param name="absoluteVarianceKg">Chênh lệch tuyệt đối kg (≥ 0).</param>
    /// <param name="isSystemZeroWithActual">true = SystemQuantity=0 nhưng đếm được hàng thực tế → luôn LARGE.</param>
    private static string ClassifySeverity(
        decimal? variancePercent,
        decimal absoluteVarianceKg,
        decimal smallPctThreshold,
        decimal mediumPctThreshold,
        decimal smallKgThreshold,
        decimal mediumKgThreshold,
        bool isSystemZeroWithActual = false)
    {
        // SystemQuantity=0 nhưng đếm được hàng thực tế là chênh lệch nghiêm trọng nhất.
        if (isSystemZeroWithActual)
            return "LARGE";

        if (variancePercent == null && absoluteVarianceKg == 0)
            return "NONE";

        // Phân loại theo %
        string severityByPct;
        if (variancePercent == null || variancePercent == 0)
            severityByPct = "NONE";
        else if (variancePercent <= smallPctThreshold)
            severityByPct = "SMALL";
        else if (variancePercent <= mediumPctThreshold)
            severityByPct = "MEDIUM";
        else
            severityByPct = "LARGE";

        // Phân loại theo kg
        string severityByKg;
        if (absoluteVarianceKg == 0)
            severityByKg = "NONE";
        else if (absoluteVarianceKg <= smallKgThreshold)
            severityByKg = "SMALL";
        else if (absoluteVarianceKg <= mediumKgThreshold)
            severityByKg = "MEDIUM";
        else
            severityByKg = "LARGE";

        // Trả về mức NẶNG HƠN trong hai tiêu chí
        return MaxSeverity(severityByPct, severityByKg);
    }

    private static string MaxSeverity(string a, string b)
    {
        var order = new[] { "NONE", "SMALL", "MEDIUM", "LARGE" };
        var idxA = Array.IndexOf(order, a);
        var idxB = Array.IndexOf(order, b);
        return idxA >= idxB ? a : b;
    }

    /// <summary>
    /// Enrich VarianceSeverity trên tất cả dòng của DTO sau khi đọc ngưỡng từ DB.
    /// </summary>
    private async Task EnrichVarianceSeverityAsync(StockTakeDto dto)
    {
        var (smallPct, mediumPct, smallKg, mediumKg) = await ResolveVarianceThresholdsAsync();
        foreach (var item in dto.StockTakeItems)
        {
            var isZeroWithActual = item.SystemQuantity == 0 && (item.ActualQuantity ?? 0) > 0;
            var absKg = item.AbsoluteVarianceKg ?? 0m;
            var severity = ClassifySeverity(item.VariancePercent, absKg, smallPct, mediumPct, smallKg, mediumKg, isZeroWithActual);

            // Lệch SỐ BAO luôn là LARGE — xem ApplyBagVarianceSeverity. Phải áp ở đây nữa
            // để mức hiển thị trên màn hình khớp với mức backend dùng lúc gửi duyệt/duyệt.
            if (item.CountedBagCount.HasValue && item.BagDifference != 0) severity = "LARGE";
            item.VarianceSeverity = severity;
        }
    }

    /// <summary>
    /// Validate danh sách dòng kiểm kê không có tổ hợp phạm vi trùng nhau.
    /// FDS: (LocationId, ProductVariantId, PaddyLotId) phải unique trong một phiếu.
    /// </summary>
    private static bool HasDuplicateItemScope(IEnumerable<(int? LocationId, int? ProductVariantId, int? PaddyLotId)> items)
    {
        var seen = new HashSet<(int?, int?, int?)>();
        foreach (var item in items)
        {
            if (!seen.Add(item)) return true;
        }
        return false;
    }

    private static string BuildScopeDisplay(IEnumerable<StockTakeItemDto> items)
    {
        var rows = items.ToList();
        if (!rows.Any()) return "Chưa có dòng";

        var lotCodes = rows.Where(x => !string.IsNullOrWhiteSpace(x.LotCode)).Select(x => x.LotCode!).Distinct().ToList();
        if (lotCodes.Count == 1 && rows.All(x => x.LotCode == lotCodes[0]))
            return $"Lô {lotCodes[0]}";

        var locations = rows.Select(x => x.LocationId).Distinct().ToList();
        if (locations.Count == 1)
            return $"{rows[0].ZoneName ?? "Khu"} / {rows[0].LocationCode ?? "Vị trí"}";

        var zones = rows.Where(x => !string.IsNullOrWhiteSpace(x.ZoneName)).Select(x => x.ZoneName!).Distinct().ToList();
        if (zones.Count == 1)
            return $"Khu {zones[0]}";

        var skus = rows.Where(x => !string.IsNullOrWhiteSpace(x.SKU)).Select(x => x.SKU!).Distinct().ToList();
        if (skus.Count == 1)
            return $"SKU {skus[0]}";

        return "Toàn kho";
    }

    /// <summary>
    /// STK-04: Kiểm tra PaddyLotId có hợp lệ không (tồn tại, đúng sản phẩm, đúng kho, đúng vị trí).
    /// Trả về error message nếu không hợp lệ, null nếu OK.
    /// </summary>
    private async Task<string?> ValidatePaddyLotRelationAsync(int paddyLotId, int? productVariantId, int warehouseId, int? locationId)
    {
        var lot = await _context.PaddyLots
            .FirstOrDefaultAsync(x => x.Id == paddyLotId && !x.IsDeleted);

        if (lot == null)
            return $"Lô hàng ID {paddyLotId} không tồn tại hoặc đã bị xóa.";

        if (productVariantId.HasValue && lot.ProductVariantId != productVariantId.Value)
            return $"Lô hàng ID {paddyLotId} không thuộc sản phẩm ID {productVariantId.Value}.";

        if (lot.WarehouseId != warehouseId)
            return $"Lô hàng ID {paddyLotId} thuộc kho ID {lot.WarehouseId}, không phải kho ID {warehouseId} của phiếu kiểm kê.";

        if (locationId.HasValue)
        {
            // Vị trí vận hành của một lô phải lấy từ dòng Inventory hiện tại.
            // PaddyLot.LocationId có thể null hoặc chưa kịp đồng bộ sau put-away/transfer,
            // vì vậy không được dùng riêng trường đó để bác bỏ một snapshot hợp lệ.
            var hasInventoryAtLocation = await _context.Inventories
                .AsNoTracking()
                .AnyAsync(x =>
                    !x.IsDeleted &&
                    x.WarehouseId == warehouseId &&
                    x.LocationId == locationId.Value &&
                    x.ProductVariantId == lot.ProductVariantId &&
                    x.PaddyLotId == paddyLotId);

            if (!hasInventoryAtLocation)
                return $"Lô hàng ID {paddyLotId} không có dòng tồn kho tại vị trí ID {locationId.Value}.";
        }

        return null; // hợp lệ
    }

    // ─── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<ApiResponse> CreateAsync(CreateStockTakeDto obj)
    {
        // Màn Stocktake mới chỉ gửi phạm vi. Backend tự sinh dòng và chụp tồn sổ sách,
        // không tin SystemQuantity do client truyền lên.
        if (!obj.StockTakeItems.Any())
        {
            // Kho gạo chỉ dán QR theo CỘT và thủ kho dỡ hàng theo cột, nên phạm vi
            // kiểm kê rút gọn còn đúng một cột. Phiếu cũ tạo theo khu/lô/toàn kho
            // vẫn mở xem được, chỉ chặn TẠO MỚI.
            var scopeType = (obj.ScopeType ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(scopeType)) scopeType = ScopeTypes.Column;
            if (scopeType != ScopeTypes.Column)
                return ApiResponse.BadRequest(
                    message: "Kiểm kê chỉ thực hiện theo CỘT. Vui lòng chọn cột cần kiểm.");

            // Validate đầu vào TRƯỚC khi dựng truy vấn: thiếu cột thì không có lý do
            // gì phải đụng tới DbSet, và dựng query trước còn làm lỗi nghiệp vụ
            // (thiếu cột) biến thành lỗi hạ tầng khi tồn kho chưa sẵn sàng.
            if (!obj.LocationId.HasValue)
                return ApiResponse.BadRequest(message: "Cột kiểm kê là bắt buộc.");

            var inventoryQuery = _context.Inventories
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.WarehouseId == obj.WarehouseId && x.LocationId != null &&
                            x.Location != null && !x.Location.IsDeleted && !x.Location.IsOutboundStaging
                            && x.LocationId == obj.LocationId);

            // Chuẩn hoá lại phạm vi lưu xuống phiếu: kiểm kê theo cột thì khu và lô
            // không còn ý nghĩa, để sót lại sẽ làm ScopeDisplay và báo cáo hiểu sai.
            obj.ScopeType = ScopeTypes.Column;
            obj.ZoneName = null;
            obj.PaddyLotId = null;
            obj.ProductVariantId = null;

            obj.StockTakeItems = await inventoryQuery
                .OrderBy(x => x.LocationId)
                .ThenBy(x => x.ProductVariantId)
                .ThenBy(x => x.PaddyLotId)
                .Select(x => new CreateStockTakeItemDto
                {
                    ProductVariantId = x.ProductVariantId,
                    LocationId = x.LocationId,
                    PaddyLotId = x.PaddyLotId,
                    ActualQuantity = null,
                    QRScanned = false,
                    RecountConfirmed = false
                })
                .ToListAsync();

            if (!obj.StockTakeItems.Any())
                return ApiResponse.UnprocessableEntity(
                    "Phạm vi đã chọn không có dòng tồn kho để kiểm kê.",
                    ApiCodeConstants.Common.UnprocessableEntity);
        }

        // STK-03 — Validate từng dòng phải có ProductVariantId và LocationId
        for (int idx = 0; idx < obj.StockTakeItems.Count; idx++)
        {
            var item = obj.StockTakeItems[idx];
            if (!item.ProductVariantId.HasValue)
                return ApiResponse.BadRequest(message: $"Dòng №{idx + 1}: ProductVariantId (Mã sản phẩm) là bắt buộc.");
            if (!item.LocationId.HasValue)
                return ApiResponse.BadRequest(message: $"Dòng №{idx + 1}: LocationId (Vị trí) là bắt buộc.");

            // STK-04 — Validate PaddyLotId nếu có cung cấp
            if (item.PaddyLotId.HasValue)
            {
                var lotError = await ValidatePaddyLotRelationAsync(
                    item.PaddyLotId.Value, item.ProductVariantId, obj.WarehouseId, item.LocationId);
                if (lotError != null)
                    return ApiResponse.BadRequest(message: $"Dòng №{idx + 1}: {lotError}");
            }
        }

        // 3.10 — Chặn dòng trùng phạm vi
        var scopes = obj.StockTakeItems
            .Select(i => (i.LocationId, i.ProductVariantId, i.PaddyLotId))
            .ToList();
        if (HasDuplicateItemScope(scopes))
            return ApiResponse.BadRequest(message: "Phiếu kiểm kê có nhiều dòng trùng phạm vi (cùng Vị trí + Sản phẩm + Lô hàng). Vui lòng hợp nhất các dòng trùng.");

        var model = obj.ToEntity();
        model.StartedDate = DateTimeHelper.VietnamNow();

        foreach (var item in model.StockTakeItems)
        {
            if (item.ProductVariantId.HasValue)
            {
                // 3.5 — Nếu có PaddyLotId: chỉ lấy tồn lô đó; nếu null: tổng tất cả dòng không có lô
                item.SystemQuantity = await _inventoryRepository
                    .FindByCondition(x =>
                        !x.IsDeleted &&
                        x.ProductVariantId == item.ProductVariantId.Value &&
                        x.WarehouseId == model.WarehouseId &&
                        x.LocationId == item.LocationId &&
                        x.PaddyLotId == item.PaddyLotId)   // exact match (null == null)
                    .SumAsync(x => (decimal?)x.QuantityOnHand) ?? 0m;
            }
        }

        // Phiếu kiểm kê ô CÁCH LY: mọi vị trí trong phạm vi đều là ô cách ly.
        // Chỉ phiếu loại này mới được rút bao đạt ra để cất về cột thường.
        var scopeLocationIds = model.StockTakeItems
            .Where(x => x.LocationId.HasValue)
            .Select(x => x.LocationId!.Value)
            .Distinct()
            .ToList();
        if (scopeLocationIds.Count > 0)
        {
            var quarantineCount = await _context.Locations.AsNoTracking()
                .CountAsync(x => scopeLocationIds.Contains(x.Id) && x.IsQuarantine);
            model.IsQuarantineScope = quarantineCount == scopeLocationIds.Count;
        }

        // Chụp ảnh từng BAO của cột — đây mới là đơn vị kiểm kê thực sự.
        await SnapshotBagsAsync(model, obj.CreatedBy);

        await _stockTakeRepository.CreateAsync(model);
        await _stockTakeRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id);
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateStockTakeDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        // ToListAsync() trước để EF materialize entities → sau đó mới gọi ToDto() trong C#.
        var entities = await _stockTakeRepository
            .FindByCondition(x => !x.IsDeleted)
            .Include(x => x.StockTakeItems)
                .ThenInclude(x => x.Bags)
            .ToListAsync();

        var (smallPct, mediumPct, smallKg, mediumKg) = await ResolveVarianceThresholdsAsync();

        var data = entities.Select(e =>
        {
            var dto = e.ToDto();
            foreach (var item in dto.StockTakeItems)
            {
                var isZeroWithActual = item.SystemQuantity == 0 && (item.ActualQuantity ?? 0) > 0;
                var absKg = item.AbsoluteVarianceKg ?? 0m;
                var severity = ClassifySeverity(item.VariancePercent, absKg, smallPct, mediumPct, smallKg, mediumKg, isZeroWithActual);
                if (item.CountedBagCount.HasValue && item.BagDifference != 0) severity = "LARGE";
                item.VarianceSeverity = severity;
            }
            return dto;
        }).ToList();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _stockTakeRepository
            .FindByCondition(x => !x.IsDeleted && x.Id == id)
            .Include(x => x.Warehouse)
            .Include(x => x.StockTakeStatus)
            .Include(x => x.StockTakeItems)
                .ThenInclude(x => x.ProductVariant)
            .Include(x => x.StockTakeItems)
                .ThenInclude(x => x.Location)
            .Include(x => x.StockTakeItems)
                .ThenInclude(x => x.PaddyLot)
            .Include(x => x.StockTakeItems)
                .ThenInclude(x => x.Bags)
                .ThenInclude(x => x.PaddyLotBag)
                .ThenInclude(x => x.Lot)
            .Include(x => x.StockTakeItems)
                .ThenInclude(x => x.Bags)
                .ThenInclude(x => x.TargetLocation)
            .FirstOrDefaultAsync();

        if (data == null)
            return ApiResponse.NotFound();

        var dto = data.ToDto();
        await EnrichVarianceSeverityAsync(dto);

        if (data.CreatedBy.HasValue)
        {
            dto.CreatedByName = await _context.Users
                .Where(x => x.Id == data.CreatedBy.Value && !x.IsDeleted)
                .Select(x => x.FirstName + " " + x.LastName)
                .FirstOrDefaultAsync();
        }

        dto.ScopeDisplay = BuildScopeDisplay(dto.StockTakeItems);
        dto.VarianceLineCount = dto.StockTakeItems.Count(x => x.ActualQuantity.HasValue && x.Difference != 0);
        dto.NetVarianceKg = dto.StockTakeItems.Where(x => x.ActualQuantity.HasValue).Sum(x => x.Difference);

        if (data.StartedDate.HasValue)
        {
            var movements = await _context.InventoryTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.WarehouseId == data.WarehouseId && x.CreatedDate > data.StartedDate.Value)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();

            foreach (var item in dto.StockTakeItems)
            {
                item.PostSnapshotMovements = movements
                    .Where(x => x.ProductVariantId == item.ProductVariantId &&
                                x.LocationId == item.LocationId &&
                                x.PaddyLotId == item.PaddyLotId)
                    .Select(x => new StockTakeMovementEvidenceDto
                    {
                        Id = x.Id,
                        TransactionType = x.TransactionType,
                        Quantity = x.Quantity,
                        BeforeQuantity = x.BeforeQuantity,
                        AfterQuantity = x.AfterQuantity,
                        Note = x.Note,
                        CreatedDate = x.CreatedDate,
                        CreatedBy = x.CreatedBy
                    })
                    .ToList();
            }
        }

        return ApiResponse.Success(dto);
    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _stockTakeRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetSummaryAsync()
    {
        var draftId = Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Draft);
        var submittedId = Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Submitted);

        var headers = _context.StockTakes.AsNoTracking().Where(x => !x.IsDeleted);
        var submittedItems = _context.StockTakeItems.AsNoTracking()
            .Where(x => !x.IsDeleted && x.StockTake.StockTakeStatusId == submittedId && x.ActualQuantity.HasValue);

        var summary = new StockTakeSummaryDto
        {
            DraftCount = await headers.CountAsync(x => x.StockTakeStatusId == draftId),
            SubmittedCount = await headers.CountAsync(x => x.StockTakeStatusId == submittedId),
            VarianceLineCount = await submittedItems.CountAsync(x => x.ActualQuantity!.Value != x.SystemQuantity),
            NetAdjustmentKg = await submittedItems.SumAsync(x => (decimal?)(x.ActualQuantity!.Value - x.SystemQuantity)) ?? 0m
        };

        return ApiResponse.Success(summary);
    }

    public async Task<ApiResponse> GetThresholdsAsync()
    {
        var (smallPct, mediumPct, smallKg, mediumKg) = await ResolveVarianceThresholdsAsync();
        return ApiResponse.Success(new StockTakeThresholdDto
        {
            SmallVariancePercent = smallPct,
            MediumVariancePercent = mediumPct,
            SmallVarianceKg = smallKg,
            MediumVarianceKg = mediumKg
        });
    }

    public async Task<ApiResponse> SaveCountsAsync(int id, SaveStockTakeCountsDto dto, int userId)
    {
        var draftId = Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Draft);
        var submittedId = Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Submitted);
        var stockTake = await _stockTakeRepository
            .FindByCondition(x => !x.IsDeleted && x.Id == id)
            .Include(x => x.StockTakeItems)
                .ThenInclude(x => x.Bags)
            .FirstOrDefaultAsync();

        if (stockTake == null) return ApiResponse.NotFound();
        if (stockTake.StockTakeStatusId != draftId)
            return ApiResponse.UnprocessableEntity(
                "Chỉ có thể cập nhật kết quả kiểm đếm khi phiếu đang ở trạng thái Nháp/Đang kiểm.",
                ApiCodeConstants.Common.UnprocessableEntity);

        var duplicateIds = dto.Items.GroupBy(x => x.Id).Where(x => x.Count() > 1).Select(x => x.Key).ToList();
        if (duplicateIds.Any())
            return ApiResponse.BadRequest(message: "Danh sách kết quả kiểm đếm có dòng bị trùng.");

        var now = DateTimeHelper.VietnamNow();
        foreach (var line in dto.Items)
        {
            var item = stockTake.StockTakeItems.FirstOrDefault(x => !x.IsDeleted && x.Id == line.Id);
            if (item == null)
                return ApiResponse.BadRequest(message: $"Dòng kiểm kê ID {line.Id} không thuộc phiếu này.");

            var duplicateBags = line.Bags
                .Where(x => x.Id > 0)
                .GroupBy(x => x.Id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicateBags.Any())
                return ApiResponse.BadRequest(message: "Kết quả kiểm đếm có bao bị gửi trùng.");

            foreach (var bagLine in line.Bags)
            {
                var row = bagLine.Id > 0
                    ? item.Bags.FirstOrDefault(x => !x.IsDeleted && x.Id == bagLine.Id)
                    : item.Bags.FirstOrDefault(x => !x.IsDeleted && x.PaddyLotBagId == bagLine.PaddyLotBagId);

                if (row == null)
                    return ApiResponse.BadRequest(
                        message: "Có bao không thuộc dòng kiểm kê này. Bao phát sinh phải được thêm bằng chức năng quét QR.");

                if (bagLine.CountedWeightKg.HasValue && bagLine.CountedWeightKg.Value < 0)
                    return ApiResponse.BadRequest(message: $"Bao #{row.BagNo}: khối lượng cân phải lớn hơn hoặc bằng 0.");
                if (!StockTakeQualityResults.IsValid(bagLine.QualityResult))
                    return ApiResponse.BadRequest(message: $"Bao #{row.BagNo}: kết quả chất lượng không hợp lệ.");

                var disposition = string.IsNullOrWhiteSpace(bagLine.Disposition)
                    ? StockTakeBagDispositions.Keep
                    : bagLine.Disposition!.Trim().ToUpperInvariant();
                if (!StockTakeBagDispositions.IsValid(disposition))
                    return ApiResponse.BadRequest(message: $"Bao #{row.BagNo}: cách xử lý '{disposition}' không hợp lệ.");
                if (disposition != StockTakeBagDispositions.Keep && !bagLine.Counted)
                    return ApiResponse.BadRequest(
                        message: $"Bao #{row.BagNo}: phải tìm thấy bao rồi mới chọn được cách xử lý.");

                row.Counted          = bagLine.Counted;
                row.ScannedByQr      = bagLine.ScannedByQr || row.ScannedByQr;
                row.CountedWeightKg  = bagLine.CountedWeightKg;
                row.QualityResult    = string.IsNullOrWhiteSpace(bagLine.QualityResult) ? null : bagLine.QualityResult;
                row.MoldLevel        = bagLine.MoldLevel;
                row.PestLevel        = bagLine.PestLevel;
                row.PackagingStatus  = bagLine.PackagingStatus;
                row.MoisturePercent  = bagLine.MoisturePercent;
                row.ImpurityPercent  = bagLine.ImpurityPercent;
                row.QualityNote      = bagLine.QualityNote?.Trim();
                row.Disposition      = disposition;
                row.TargetLocationId = bagLine.TargetLocationId;
                row.DispositionNote  = bagLine.DispositionNote?.Trim();
                row.LastModifiedDate = now;
                row.UpdatedBy        = userId;
            }

            if (line.AdjustedBagCount.HasValue && line.AdjustedBagCount.Value < 0)
                return ApiResponse.BadRequest(message: "Số bao chỉnh lý phải lớn hơn hoặc bằng 0.");
            if (line.AdjustedWeightKg.HasValue && line.AdjustedWeightKg.Value < 0)
                return ApiResponse.BadRequest(message: "Khối lượng chỉnh lý phải lớn hơn hoặc bằng 0.");

            item.Note             = line.Note?.Trim();
            item.VarianceReason   = line.VarianceReason?.Trim();
            item.AdjustedBagCount = line.AdjustedBagCount;
            item.AdjustedWeightKg = line.AdjustedWeightKg;
            item.LastModifiedDate = now;
            item.UpdatedBy        = userId;

            if (item.Bags.Any(x => !x.IsDeleted))
            {
                item.QRScanned = item.Bags.Any(x => !x.IsDeleted && x.ScannedByQr);
                RecomputeItemTotals(item);
            }
            else
            {
                // Dòng tồn kho cũ chưa quản lý theo bao — vẫn cho nhập kg tay.
                if (line.ActualQuantity.HasValue && line.ActualQuantity.Value < 0)
                    return ApiResponse.BadRequest(message: "Số lượng kiểm đếm phải lớn hơn hoặc bằng 0.");
                item.ActualQuantity = line.ActualQuantity;
            }

            if (!item.RecountConfirmed && line.RecountConfirmed)
            {
                item.RecountConfirmed = true;
                item.RecountConfirmedBy = userId;
                item.RecountConfirmedAt = now;
            }
            else if (item.RecountConfirmed && !line.RecountConfirmed)
            {
                item.RecountConfirmed = false;
                item.RecountConfirmedBy = null;
                item.RecountConfirmedAt = null;
            }
        }

        // Tự điền vị trí đích cho bao chuyển cách ly / rút cách ly nếu người dùng chưa chọn.
        await FillMissingBagTargetsAsync(stockTake, userId, now);

        var unfinished = stockTake.StockTakeItems
            .Where(x => !x.IsDeleted &&
                        (x.Bags.Any(b => !b.IsDeleted) ? !x.CountedBagCount.HasValue : !x.ActualQuantity.HasValue))
            .ToList();
        if (unfinished.Any())
            return ApiResponse.UnprocessableEntity(
                $"Phiếu còn {unfinished.Count} dòng chưa kiểm đếm bao nào.",
                ApiCodeConstants.Common.UnprocessableEntity);

        stockTake.Note = dto.Note?.Trim() ?? stockTake.Note;
        stockTake.StockTakeStatusId = submittedId;
        stockTake.LastModifiedDate = now;
        stockTake.UpdatedBy = userId;
        await _stockTakeRepository.UpdateAsync(stockTake);
        await _stockTakeRepository.SaveChangesAsync();
        return ApiResponse.Success(message: "Đã lưu và gửi phiếu kiểm kê để Chủ kho duyệt.");
    }

    public async Task<ApiResponse> SubmitAsync(int id, SubmitStockTakeDto dto, int userId)
    {
        var draftId = Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Draft);
        var submittedId = Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Submitted);
        var stockTake = await _stockTakeRepository
            .FindByCondition(x => !x.IsDeleted && x.Id == id)
            .Include(x => x.StockTakeItems)
                .ThenInclude(x => x.ProductVariant)
            .Include(x => x.StockTakeItems)
                .ThenInclude(x => x.Bags)
            .FirstOrDefaultAsync();

        if (stockTake == null) return ApiResponse.NotFound();
        if (stockTake.StockTakeStatusId != draftId)
            return ApiResponse.UnprocessableEntity(
                "Chỉ có thể gửi duyệt phiếu đang ở trạng thái Nháp/Đang kiểm.",
                ApiCodeConstants.Common.UnprocessableEntity);
        if (!stockTake.StockTakeItems.Any(x => !x.IsDeleted))
            return ApiResponse.UnprocessableEntity("Phiếu kiểm kê không có dòng nào.", ApiCodeConstants.Common.UnprocessableEntity);

        var now = DateTimeHelper.VietnamNow();

        // Dòng theo bao chưa đụng bao nào coi như chưa kiểm — tick tổng kg không thay được việc đếm bao.
        var unfinished = stockTake.StockTakeItems
            .Where(x => !x.IsDeleted &&
                        (x.Bags.Any(b => !b.IsDeleted) ? !x.CountedBagCount.HasValue : !x.ActualQuantity.HasValue))
            .ToList();
        if (unfinished.Any())
            return ApiResponse.UnprocessableEntity(
                $"Phiếu còn {unfinished.Count} dòng chưa kiểm đếm bao nào.",
                ApiCodeConstants.Common.UnprocessableEntity);

        var dispositionError = await ValidateAndFillBagDispositionsAsync(stockTake, userId, now);
        if (dispositionError != null)
            return ApiResponse.UnprocessableEntity(dispositionError, ApiCodeConstants.Common.UnprocessableEntity);

        var (smallPct, mediumPct, smallKg, mediumKg) = await ResolveVarianceThresholdsAsync();
        var largeItems = new List<StockTakeItem>();
        foreach (var item in stockTake.StockTakeItems.Where(x => !x.IsDeleted && x.ActualQuantity.HasValue))
        {
            var name = item.ProductVariant?.Name ?? $"ProductVariantId={item.ProductVariantId}";
            if (item.Difference == 0 && !item.HasBagVariance) continue;

            var isZeroWithActual = item.SystemQuantity == 0 && item.ActualQuantity!.Value > 0;
            var severity = ApplyBagVarianceSeverity(item, ClassifySeverity(
                item.VariancePercent, item.AbsoluteVarianceKg,
                smallPct, mediumPct, smallKg, mediumKg, isZeroWithActual));

            // Lệch số bao hoặc lệch kg đều BẮT BUỘC nêu lý do — đây là yêu cầu nghiệp vụ,
            // không phụ thuộc mức độ chênh lệch.
            if (string.IsNullOrWhiteSpace(item.VarianceReason))
                return ApiResponse.UnprocessableEntity(
                    item.HasBagVariance
                        ? $"Dòng '{name}' lệch {Math.Abs(item.BagDifference)} bao — bắt buộc nêu lý do trước khi gửi duyệt."
                        : $"Dòng '{name}' lệch khối lượng — bắt buộc nêu lý do trước khi gửi duyệt.",
                    ApiCodeConstants.Common.UnprocessableEntity);

            if (severity == "LARGE" && !item.RecountConfirmed)
                return ApiResponse.UnprocessableEntity(
                    $"Dòng '{name}' có chênh lệch LARGE — bắt buộc xác nhận kiểm đếm lại.",
                    ApiCodeConstants.Common.UnprocessableEntity);
            if (severity == "LARGE") largeItems.Add(item);
        }

        await using var transaction = await _stockTakeRepository.BeginTransactionAsync();
        try
        {
            stockTake.StockTakeStatusId = submittedId;
            stockTake.Note = dto.Note?.Trim() ?? stockTake.Note;
            stockTake.LastModifiedDate = now;
            stockTake.UpdatedBy = userId;

            foreach (var item in largeItems)
            {
                var dedupKey = $"STOCKTAKE_LARGE_{stockTake.Id}_{item.Id}";
                if (await _context.Alerts.AnyAsync(x => !x.IsDeleted && x.DeduplicationKey == dedupKey && x.Status != "RESOLVED"))
                    continue;

                await _context.Alerts.AddAsync(new Alert
                {
                    AlertType = "STOCK_TAKE_LARGE_VARIANCE",
                    Severity = "CRITICAL",
                    WarehouseId = stockTake.WarehouseId,
                    ProductVariantId = item.ProductVariantId,
                    LocationId = item.LocationId,
                    Message = $"[{stockTake.STCode}] Chênh lệch LARGE: {item.ProductVariant?.Name ?? $"PV#{item.ProductVariantId}"} — " +
                              $"Bao: {item.SystemBagCount} → {item.CountedBagCount}, " +
                              $"Kg: {item.SystemQuantity:F2} → {item.ActualQuantity:F2}. Lý do: {item.VarianceReason}",
                    RelatedEntityType = "StockTakeItem",
                    RelatedEntityId = item.Id,
                    Status = "OPEN",
                    DeduplicationKey = dedupKey,
                    CreatedDate = now,
                    CreatedBy = userId
                });
            }

            await _stockTakeRepository.UpdateAsync(stockTake);
            await _stockTakeRepository.SaveChangesAsync();
            if (largeItems.Any()) await _context.SaveChangesAsync(default);
            await transaction.CommitAsync();
            return ApiResponse.Success(message: "Đã gửi phiếu kiểm kê để Chủ kho duyệt.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var existData = await _stockTakeRepository.FindByCondition(x => x.Id == id).FirstOrDefaultAsync();
        if (existData == null)
            return ApiResponse.NotFound();

        if (existData.StockTakeStatusId == Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Approved) ||
            existData.StockTakeStatusId == Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Rejected))
        {
            return ApiResponse.UnprocessableEntity("Không thể xóa phiếu đã duyệt hoặc bị từ chối.", ApiCodeConstants.Common.UnprocessableEntity);
        }

        if (existData.StockTakeStatusId != Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Draft))
        {
            return ApiResponse.UnprocessableEntity(
                "Phiếu đã gửi duyệt được khóa để bảo toàn bằng chứng kiểm kê.",
                ApiCodeConstants.Common.UnprocessableEntity);
        }

        var isDeleted = await _stockTakeRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _stockTakeRepository.SaveChangesAsync();
        return ApiResponse.Success();
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateAsync(UpdateStockTakeDto obj)
    {
        var existData = await _stockTakeRepository
            .FindByCondition(x => !x.IsDeleted && x.Id == obj.Id)
            .Include(x => x.StockTakeItems)
            .FirstOrDefaultAsync();

        if (existData == null)
            return ApiResponse.NotFound();

        if (existData.StockTakeStatusId == Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Approved) ||
            existData.StockTakeStatusId == Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Rejected))
        {
            return ApiResponse.UnprocessableEntity("Không thể cập nhật phiếu đã duyệt hoặc bị từ chối.", ApiCodeConstants.Common.UnprocessableEntity);
        }

        if (existData.StockTakeStatusId != Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Draft))
        {
            return ApiResponse.UnprocessableEntity(
                "Phiếu đã gửi duyệt được khóa để bảo toàn bằng chứng kiểm kê.",
                ApiCodeConstants.Common.UnprocessableEntity);
        }

        if (obj.StockTakeStatusId != Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Draft))
        {
            return ApiResponse.UnprocessableEntity(
                "Hãy dùng chức năng Gửi duyệt/Duyệt/Từ chối để chuyển trạng thái phiếu.",
                ApiCodeConstants.Common.UnprocessableEntity);
        }

        // STK-03 — Validate từng dòng phải có ProductVariantId và LocationId
        for (int idx = 0; idx < obj.StockTakeItems.Count; idx++)
        {
            var itemDto = obj.StockTakeItems[idx];
            if (!itemDto.ProductVariantId.HasValue)
                return ApiResponse.BadRequest(message: $"Dòng №{idx + 1}: ProductVariantId (Mã sản phẩm) là bắt buộc.");
            if (!itemDto.LocationId.HasValue)
                return ApiResponse.BadRequest(message: $"Dòng №{idx + 1}: LocationId (Vị trí) là bắt buộc.");
        }

        // 3.10 — Validate không có tổ hợp phạm vi trùng
        var allScopes = obj.StockTakeItems
            .Select(i => (i.LocationId, i.ProductVariantId, i.PaddyLotId))
            .ToList();
        if (HasDuplicateItemScope(allScopes))
            return ApiResponse.BadRequest(message: "Phiếu kiểm kê có nhiều dòng trùng phạm vi. Vui lòng hợp nhất các dòng trùng.");

        obj.ToEntity(existData);

        // 1. Xóa dòng không còn trong DTO
        var dtoItemIds = obj.StockTakeItems.Where(i => i.Id > 0).Select(i => i.Id).ToList();
        var itemsToDelete = existData.StockTakeItems.Where(i => !dtoItemIds.Contains(i.Id)).ToList();
        foreach (var item in itemsToDelete)
            await _stockTakeItemRepository.HardDeleteAsync(item.Id);

        // 2. Cập nhật dòng hiện có
        var now = DateTimeHelper.VietnamNow();
        var currentUserId = _httpContextAccessor.HttpContext?.GetCurrentUserId();
        foreach (var itemDto in obj.StockTakeItems.Where(i => i.Id > 0))
        {
            var existingItem = existData.StockTakeItems.FirstOrDefault(i => i.Id == itemDto.Id);
            if (existingItem != null)
            {
                // 3.6 — Nếu phạm vi thay đổi thì tính lại SystemQuantity
                bool scopeChanged =
                    existingItem.ProductVariantId != itemDto.ProductVariantId ||
                    existingItem.LocationId != itemDto.LocationId ||
                    existingItem.PaddyLotId != itemDto.PaddyLotId;

                if (scopeChanged && itemDto.PaddyLotId.HasValue)
                {
                    var lotError = await ValidatePaddyLotRelationAsync(
                        itemDto.PaddyLotId.Value, itemDto.ProductVariantId, existData.WarehouseId, itemDto.LocationId);
                    if (lotError != null)
                        return ApiResponse.BadRequest(message: $"Dòng ID {itemDto.Id}: {lotError}");
                }

                existingItem.ProductVariantId   = itemDto.ProductVariantId;
                existingItem.LocationId         = itemDto.LocationId;
                existingItem.PaddyLotId         = itemDto.PaddyLotId;
                existingItem.ActualQuantity      = itemDto.ActualQuantity;
                existingItem.Note               = itemDto.Note;
                existingItem.QRScanned          = itemDto.QRScanned;
                existingItem.LastModifiedDate   = now;

                // STK-02 — Backend tự ghi audit khi RecountConfirmed thay đổi
                if (!existingItem.RecountConfirmed && itemDto.RecountConfirmed)
                {
                    // false → true: ghi thông tin xác nhận
                    existingItem.RecountConfirmed   = true;
                    existingItem.RecountConfirmedBy = currentUserId;
                    existingItem.RecountConfirmedAt = now;
                }
                else if (existingItem.RecountConfirmed && !itemDto.RecountConfirmed)
                {
                    // true → false: xóa thông tin xác nhận (rút xác nhận)
                    existingItem.RecountConfirmed   = false;
                    existingItem.RecountConfirmedBy = null;
                    existingItem.RecountConfirmedAt = null;
                }
                // Giữ nguyên nếu không thay đổi (giảm write xã)

                if (scopeChanged && itemDto.ProductVariantId.HasValue)
                {
                    existingItem.SystemQuantity = await _inventoryRepository
                        .FindByCondition(x =>
                            !x.IsDeleted &&
                            x.ProductVariantId == itemDto.ProductVariantId.Value &&
                            x.WarehouseId == existData.WarehouseId &&
                            x.LocationId == itemDto.LocationId &&
                            x.PaddyLotId == itemDto.PaddyLotId)
                        .SumAsync(x => (decimal?)x.QuantityOnHand) ?? 0m;
                }
            }
        }

        // 3. Thêm dòng mới
        var newItems = new List<StockTakeItem>();
        int newIdx = 0;
        foreach (var itemDto in obj.StockTakeItems.Where(i => i.Id == 0))
        {
            newIdx++;
            // STK-04 — Validate PaddyLotId đối với dòng mới
            if (itemDto.PaddyLotId.HasValue)
            {
                var lotError = await ValidatePaddyLotRelationAsync(
                    itemDto.PaddyLotId.Value, itemDto.ProductVariantId, existData.WarehouseId, itemDto.LocationId);
                if (lotError != null)
                    return ApiResponse.BadRequest(message: $"Dòng mới №{newIdx}: {lotError}");
            }

            decimal sysQty = 0;
            if (itemDto.ProductVariantId.HasValue)
            {
                sysQty = await _inventoryRepository
                    .FindByCondition(x =>
                        !x.IsDeleted &&
                        x.ProductVariantId == itemDto.ProductVariantId.Value &&
                        x.WarehouseId == existData.WarehouseId &&
                        x.LocationId == itemDto.LocationId &&
                        x.PaddyLotId == itemDto.PaddyLotId)
                    .SumAsync(x => (decimal?)x.QuantityOnHand) ?? 0m;
            }

            newItems.Add(new StockTakeItem
            {
                StockTakeId      = existData.Id,
                ProductVariantId = itemDto.ProductVariantId,
                LocationId       = itemDto.LocationId,
                PaddyLotId       = itemDto.PaddyLotId,
                SystemQuantity   = sysQty,
                ActualQuantity   = itemDto.ActualQuantity,
                Note             = itemDto.Note,
                QRScanned        = itemDto.QRScanned,
                RecountConfirmed = false,
                CreatedDate      = now
            });
        }

        if (newItems.Any())
            await _stockTakeItemRepository.CreateListAsync(newItems);

        await _stockTakeRepository.UpdateAsync(existData);
        await _stockTakeRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateStockTakeDto> obj)
    {
        throw new NotImplementedException();
    }

    // ─── Approve ──────────────────────────────────────────────────────────────

    public async Task<ApiResponse> ApproveAsync(int id, string? approveNote, int userId)
    {
        // 3.12 — FDS: chỉ OWNER duyệt; ADMIN là super-admin theo quy ước toàn dự án → giữ cả hai
        var currentRoleIds = _httpContextAccessor.HttpContext?.GetCurrentRoleIds() ?? new List<int>();
        if (!currentRoleIds.Contains(CommonConstants.Role.ADMIN) && !currentRoleIds.Contains(CommonConstants.Role.OWNER))
        {
            return ApiResponse.Forbidden(message: "Forbidden");
        }

        var existData = await _stockTakeRepository
            .FindByCondition(x => !x.IsDeleted && x.Id == id)
            .Include(x => x.StockTakeItems)
                .ThenInclude(i => i.ProductVariant)
            .Include(x => x.StockTakeItems)
                .ThenInclude(i => i.Location)
            .Include(x => x.StockTakeItems)
                .ThenInclude(i => i.Bags)
            .FirstOrDefaultAsync();

        if (existData == null)
            return ApiResponse.NotFound();

        // Tách quyền (segregation of duties): người tạo phiếu không được tự duyệt
        // phiếu kiểm kê do chính mình tạo, kể cả khi vai trò có quyền APPROVE.
        if (existData.CreatedBy.HasValue && existData.CreatedBy.Value == userId)
            return ApiResponse.Forbidden(message: "Bạn không thể tự duyệt phiếu kiểm kê do chính mình tạo.", code: ApiCodeConstants.Common.Forbidden);

        if (existData.StockTakeStatusId != Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Submitted))
            return ApiResponse.UnprocessableEntity("Chỉ có thể duyệt phiếu kiểm kho ở trạng thái chờ duyệt.", ApiCodeConstants.Common.UnprocessableEntity);

        if (!existData.StockTakeItems.Any(x => !x.IsDeleted))
            return ApiResponse.UnprocessableEntity("Phiếu kiểm kê không có dòng nào. Không thể duyệt phiếu trống.", ApiCodeConstants.Common.UnprocessableEntity);

        var itemsMissingScope = existData.StockTakeItems
            .Where(i => !i.IsDeleted && (!i.ProductVariantId.HasValue || !i.LocationId.HasValue))
            .ToList();
        if (itemsMissingScope.Any())
            return ApiResponse.UnprocessableEntity(
                $"Phiếu kiểm kê có {itemsMissingScope.Count} dòng thiếu thông tin bắt buộc (Sản phẩm hoặc Vị trí). " +
                "Vui lòng bổ sung trước khi duyệt.",
                ApiCodeConstants.Common.UnprocessableEntity);

        var unfinishedItems = existData.StockTakeItems
            .Where(i => !i.IsDeleted &&
                        (i.Bags.Any(b => !b.IsDeleted) ? !i.CountedBagCount.HasValue : !i.ActualQuantity.HasValue))
            .ToList();
        if (unfinishedItems.Any())
        {
            var names = unfinishedItems
                .Select(i => i.ProductVariant?.Name ?? $"ProductVariantId={i.ProductVariantId}")
                .Take(3);
            return ApiResponse.UnprocessableEntity(
                $"Phiếu còn {unfinishedItems.Count} dòng chưa kiểm đếm ({string.Join(", ", names)}...). Vui lòng kiểm đủ trước khi duyệt.",
                ApiCodeConstants.Common.UnprocessableEntity);
        }

        var stagingVarianceItems = existData.StockTakeItems
            .Where(i => !i.IsDeleted && i.Location?.IsOutboundStaging == true && i.Difference != 0)
            .ToList();
        if (stagingVarianceItems.Any())
        {
            return ApiResponse.Conflict(
                $"Có {stagingVarianceItems.Count} dòng chênh lệch tại khu Chờ xuất. " +
                "Không thể điều chỉnh tồn trực tiếp vì hàng đang gắn với phiếu xuất và bao đã đóng gói. " +
                "Vui lòng đối chiếu phiếu xuất/bao nguồn trước khi xử lý chênh lệch.",
                ApiCodeConstants.Common.DuplicatedData);
        }

        var now = DateTimeHelper.VietnamNow();

        var dispositionError = await ValidateAndFillBagDispositionsAsync(existData, userId, now);
        if (dispositionError != null)
            return ApiResponse.UnprocessableEntity(dispositionError, ApiCodeConstants.Common.UnprocessableEntity);

        // Mở transaction trước bước kiểm tra snapshot. RowVersion của Inventory tiếp tục
        // bảo vệ trường hợp một giao dịch khác chen vào giữa bước kiểm tra và bước ghi.
        await using var transaction = await _stockTakeRepository.BeginTransactionAsync();

        var (smallPct, mediumPct, smallKg, mediumKg) = await ResolveVarianceThresholdsAsync();

        foreach (var item in existData.StockTakeItems.Where(x => !x.IsDeleted))
        {
            if (!item.ActualQuantity.HasValue || !item.ProductVariantId.HasValue)
                continue;

            var hasBags = item.Bags.Any(b => !b.IsDeleted);
            var variantName = item.ProductVariant?.Name ?? $"ProductVariantId={item.ProductVariantId}";

            // STK-05 — Concurrency: xử lý cả trường hợp dòng tồn bị xóa (null → 0)
            var currentInventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                item.ProductVariantId.Value, existData.WarehouseId, item.LocationId, item.PaddyLotId);
            var currentQtyOnHand = currentInventory?.QuantityOnHand ?? 0m;
            if (currentQtyOnHand != item.SystemQuantity)
            {
                return ApiResponse.Conflict(
                    $"Tồn kho của '{variantName}' đã thay đổi từ {item.SystemQuantity} thành {currentQtyOnHand} sau khi lập phiếu. " +
                    "Vui lòng tải lại phiếu kiểm kê và cập nhật số liệu trước khi duyệt.");
            }

            var hasPostSnapshotMovement = existData.StartedDate.HasValue &&
                await _context.InventoryTransactions.AsNoTracking().AnyAsync(x =>
                    !x.IsDeleted &&
                    x.WarehouseId == existData.WarehouseId &&
                    x.ProductVariantId == item.ProductVariantId &&
                    x.LocationId == item.LocationId &&
                    x.PaddyLotId == item.PaddyLotId &&
                    x.CreatedDate > existData.StartedDate.Value &&
                    !(x.ReferenceType == InventoryReferenceTypeConstants.StockTake && x.ReferenceId == existData.Id));
            if (hasPostSnapshotMovement)
            {
                return ApiResponse.Conflict(
                    $"Đã phát sinh giao dịch tồn kho cho '{variantName}' sau thời điểm chụp số liệu. " +
                    "Vui lòng lập lại phiếu hoặc cập nhật snapshot trước khi duyệt.");
            }

            // 3.11 — Tồn còn lại tại vị trí sau kiểm kê không được nhỏ hơn phần đang đặt giữ.
            if (currentInventory != null)
            {
                var newQty = hasBags ? RemainingWeightAtLocation(item) : item.ActualQuantity!.Value;
                if (newQty < currentInventory.QuantityReserved)
                {
                    return ApiResponse.UnprocessableEntity(
                        $"Không thể điều chỉnh tồn '{variantName}' xuống {newQty:0.###} kg vì đang có {currentInventory.QuantityReserved:0.###} kg đặt giữ. " +
                        "Vui lòng giải phóng các đơn liên quan trước khi duyệt.",
                        ApiCodeConstants.Common.UnprocessableEntity);
                }
            }

            if (item.Difference == 0 && !item.HasBagVariance) continue;

            var isZeroWithActual = item.SystemQuantity == 0 && (item.ActualQuantity ?? 0) > 0;
            var severity = ApplyBagVarianceSeverity(item, ClassifySeverity(
                item.VariancePercent, item.AbsoluteVarianceKg,
                smallPct, mediumPct, smallKg, mediumKg, isZeroWithActual));

            if (string.IsNullOrWhiteSpace(item.VarianceReason))
                return ApiResponse.UnprocessableEntity(
                    $"Dòng kiểm kê '{variantName}' có chênh lệch — bắt buộc nêu lý do trước khi duyệt.",
                    ApiCodeConstants.Common.UnprocessableEntity);

            if (severity == "LARGE" && !item.RecountConfirmed)
                return ApiResponse.UnprocessableEntity(
                    $"Dòng kiểm kê '{variantName}' có mức chênh lệch LARGE — bắt buộc xác nhận kiểm đếm lại trước khi duyệt.",
                    ApiCodeConstants.Common.UnprocessableEntity);
        }

        try
        {
            existData.StockTakeStatusId  = Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Approved);
            existData.ApprovedByUserId   = userId;
            existData.ApproveNote        = approveNote;
            existData.CompletedDate      = now;
            existData.LastModifiedDate   = now;
            existData.UpdatedBy          = userId;

            // ── B1: áp kết quả xuống BAO VẬT LÝ trước ─────────────────────────
            var sync = await SyncPhysicalBagsAsync(existData, userId, now);

            // ── B2: tính lại tồn kho TỪ khối lượng đang nằm trong bao ─────────
            // Làm theo hướng này thì bất biến SUM(kg trong bao) == Inventory không bao giờ vỡ,
            // kể cả khi bao chuyển sang ô cách ly hay bị bỏ vì hỏng.
            foreach (var key in sync.TouchedInventories)
            {
                var targetKg = await SumBagContentWeightAsync(key.PaddyLotId, key.LocationId);
                var current = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                    key.ProductVariantId, existData.WarehouseId, key.LocationId, key.PaddyLotId);
                var currentKg = current?.QuantityOnHand ?? 0m;
                if (Math.Abs(currentKg - targetKg) <= 0.001m) continue;

                var result = await _inventoryTransactionService.AdjustStockAsync(new DTOs.InventoryTransactions.StockMovementRequestDto
                {
                    ProductVariantId = key.ProductVariantId,
                    WarehouseId      = existData.WarehouseId,
                    LocationId       = key.LocationId,
                    PaddyLotId       = key.PaddyLotId,
                    Quantity         = Math.Abs(targetKg - currentKg),
                    ReferenceType    = InventoryReferenceTypeConstants.StockTake,
                    ReferenceId      = existData.Id,
                    Note             = $"Kiểm kê {existData.STCode}: đặt lại tồn theo khối lượng thực trong bao"
                }, targetKg, true);

                if (!result.IsSucceeded)
                {
                    await transaction.RollbackAsync();
                    if (result.Status == 409)
                        return ApiResponse.Conflict($"Xung đột khi điều chỉnh tồn kho: {result.Message}");
                    return ApiResponse.UnprocessableEntity($"Lỗi điều chỉnh tồn kho: {result.Message}", ApiCodeConstants.Common.UnprocessableEntity);
                }
            }

            // ── B3: dòng tồn kho CŨ chưa quản lý theo bao — vẫn chỉnh theo kg tay ──
            var largeItems = new List<(StockTakeItem item, string severity)>();
            foreach (var item in existData.StockTakeItems.Where(x => !x.IsDeleted))
            {
                if (!item.ActualQuantity.HasValue || !item.ProductVariantId.HasValue) continue;

                var isZeroWithActual = item.SystemQuantity == 0 && (item.ActualQuantity ?? 0) > 0;
                var severity = ApplyBagVarianceSeverity(item, ClassifySeverity(
                    item.VariancePercent, item.AbsoluteVarianceKg,
                    smallPct, mediumPct, smallKg, mediumKg, isZeroWithActual));
                if (severity == "LARGE") largeItems.Add((item, severity));

                if (item.Bags.Any(b => !b.IsDeleted)) continue;   // đã xử lý ở B2
                if (item.Difference == 0) continue;

                var noteText = $"Điều chỉnh kiểm kho {existData.STCode} — {severity}";
                if (!string.IsNullOrWhiteSpace(item.VarianceReason))
                    noteText += $" — Lý do: {item.VarianceReason}";

                var legacyResult = await _inventoryTransactionService.AdjustStockAsync(new DTOs.InventoryTransactions.StockMovementRequestDto
                {
                    ProductVariantId = item.ProductVariantId.Value,
                    WarehouseId      = existData.WarehouseId,
                    LocationId       = item.LocationId,
                    PaddyLotId       = item.PaddyLotId,
                    Quantity         = Math.Abs(item.Difference),
                    ReferenceType    = InventoryReferenceTypeConstants.StockTake,
                    ReferenceId      = existData.Id,
                    ReferenceItemId  = item.Id,
                    Note             = noteText
                }, item.ActualQuantity.Value, true);

                if (!legacyResult.IsSucceeded)
                {
                    await transaction.RollbackAsync();
                    if (legacyResult.Status == 409)
                        return ApiResponse.Conflict($"Xung đột khi điều chỉnh tồn kho: {legacyResult.Message}");
                    return ApiResponse.UnprocessableEntity($"Lỗi điều chỉnh tồn kho: {legacyResult.Message}", ApiCodeConstants.Common.UnprocessableEntity);
                }
            }

            // ── B4: cất lại cột + đồng bộ sức chứa ────────────────────────────
            await RestowColumnsAsync(sync.TouchedLocations, existData.Id, existData.STCode, userId, now);
            await SyncLocationOccupancyAsync(sync.TouchedLocations, userId, now);

            // ── B5: lưu vết chất lượng sang hồ sơ Kiểm định ──────────────────
            await CreateQualityTraceAsync(existData, userId, now);

            await _stockTakeRepository.UpdateAsync(existData);
            await _stockTakeRepository.SaveChangesAsync();

            // 3.4 — Alert CRITICAL cho từng dòng LARGE, trong cùng transaction
            foreach (var (item, severity) in largeItems)
            {
                var dedupKey = $"STOCKTAKE_LARGE_{existData.Id}_{item.Id}";
                if (await _context.Alerts.AnyAsync(x =>
                        !x.IsDeleted && x.DeduplicationKey == dedupKey && x.Status != "RESOLVED"))
                    continue;

                var lotInfo = item.PaddyLotId.HasValue ? $" (Lô #{item.PaddyLotId})" : "";
                await _context.Alerts.AddAsync(new Alert
                {
                    AlertType          = "STOCK_TAKE_LARGE_VARIANCE",
                    Severity           = "CRITICAL",
                    WarehouseId        = existData.WarehouseId,
                    ProductVariantId   = item.ProductVariantId,
                    LocationId         = item.LocationId,
                    Message            = $"[{existData.STCode}] Chênh lệch LARGE: {item.ProductVariant?.Name ?? $"PV#{item.ProductVariantId}"}{lotInfo} — " +
                                         $"Bao: {item.SystemBagCount} → {item.CountedBagCount}, " +
                                         $"Kg: {item.SystemQuantity:F2} → {item.ActualQuantity:F2}. " +
                                         $"Lý do: {item.VarianceReason ?? "(chưa ghi)"}",
                    RelatedEntityType  = "StockTakeItem",
                    RelatedEntityId    = item.Id,
                    Status             = "OPEN",
                    DeduplicationKey   = dedupKey,
                    CreatedDate        = now,
                    CreatedBy          = userId
                });
            }

            // Bao chuyển cách ly là sự cố chất lượng — cảnh báo riêng để đội kiểm định biết.
            if (sync.QuarantinedBags > 0 || sync.DisposedBags > 0)
            {
                var qualityKey = $"STOCKTAKE_QUALITY_{existData.Id}";
                if (!await _context.Alerts.AnyAsync(x => !x.IsDeleted && x.DeduplicationKey == qualityKey && x.Status != "RESOLVED"))
                {
                    await _context.Alerts.AddAsync(new Alert
                    {
                        AlertType         = "STOCK_TAKE_QUALITY_ISSUE",
                        Severity          = "HIGH",
                        WarehouseId       = existData.WarehouseId,
                        Message           = $"[{existData.STCode}] Kiểm kê phát hiện {sync.QuarantinedBags} bao chuyển cách ly, " +
                                            $"{sync.DisposedBags} bao hỏng bị loại bỏ.",
                        RelatedEntityType = "StockTake",
                        RelatedEntityId   = existData.Id,
                        Status            = "OPEN",
                        DeduplicationKey  = qualityKey,
                        CreatedDate       = now,
                        CreatedBy         = userId
                    });
                }
            }

            await _context.SaveChangesAsync(default);
            await transaction.CommitAsync();

            await _notificationDispatcher.DispatchAsync(
                NotificationConstants.Code.StockTakeApproved,
                new NotificationTarget
                {
                    UserIds = existData.CreatedBy.HasValue ? new List<int> { existData.CreatedBy.Value } : new List<int>(),
                },
                new object[] { existData.STCode },
                "/admin/stock-takes",
                userId);

            return ApiResponse.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return ApiResponse.Conflict(
                "Tồn kho vừa được cập nhật bởi giao dịch khác. Phiếu chưa được duyệt; vui lòng tải lại dữ liệu.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ─── Reject ───────────────────────────────────────────────────────────────

    public async Task<ApiResponse> RejectAsync(int id, string reason, int userId)
    {
        var currentRoleIds = _httpContextAccessor.HttpContext?.GetCurrentRoleIds() ?? new List<int>();
        if (!currentRoleIds.Contains(CommonConstants.Role.ADMIN) && !currentRoleIds.Contains(CommonConstants.Role.OWNER))
        {
            return ApiResponse.Forbidden(message: "Forbidden");
        }





        var existData = await _stockTakeRepository.FindByCondition(x => !x.IsDeleted && x.Id == id).FirstOrDefaultAsync();
        if (existData == null)
            return ApiResponse.NotFound();

        if (existData.StockTakeStatusId != Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Submitted))
            return ApiResponse.UnprocessableEntity("Chỉ có thể từ chối phiếu kiểm kho ở trạng thái chờ duyệt.", ApiCodeConstants.Common.UnprocessableEntity);

        await using var transaction = await _stockTakeRepository.BeginTransactionAsync();
        try
        {
            existData.StockTakeStatusId  = Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Rejected);
            existData.Note               = string.IsNullOrWhiteSpace(existData.Note) ? $"Từ chối: {reason}" : $"{existData.Note} | Từ chối: {reason}";
            existData.ApproveNote        = reason;
            existData.CompletedDate      = DateTimeHelper.VietnamNow();
            existData.LastModifiedDate   = DateTimeHelper.VietnamNow();
            existData.UpdatedBy          = userId;

            await _stockTakeRepository.UpdateAsync(existData);
            await _stockTakeRepository.SaveChangesAsync();
            await transaction.CommitAsync();

            await _notificationDispatcher.DispatchAsync(
                NotificationConstants.Code.StockTakeRejected,
                new NotificationTarget
                {
                    UserIds = existData.CreatedBy.HasValue ? new List<int> { existData.CreatedBy.Value } : new List<int>(),
                },
                new object[] { existData.STCode, reason },
                "/admin/stock-takes",
                userId);

            return ApiResponse.Success();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
