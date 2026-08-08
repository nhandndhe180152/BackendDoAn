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

public class StockTakeService : IStockTakeService
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
            item.VarianceSeverity = ClassifySeverity(item.VariancePercent, absKg, smallPct, mediumPct, smallKg, mediumKg, isZeroWithActual);
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

        if (locationId.HasValue && lot.LocationId != locationId.Value)
            return $"Lô hàng ID {paddyLotId} hiện nằm ở vị trí ID {lot.LocationId}, không phải vị trí ID {locationId.Value} trong dòng kiểm kê.";

        return null; // hợp lệ
    }

    // ─── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<ApiResponse> CreateAsync(CreateStockTakeDto obj)
    {
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
            .ToListAsync();

        var (smallPct, mediumPct, smallKg, mediumKg) = await ResolveVarianceThresholdsAsync();

        var data = entities.Select(e =>
        {
            var dto = e.ToDto();
            foreach (var item in dto.StockTakeItems)
            {
                var isZeroWithActual = item.SystemQuantity == 0 && (item.ActualQuantity ?? 0) > 0;
                var absKg = item.AbsoluteVarianceKg ?? 0m;
                item.VarianceSeverity = ClassifySeverity(item.VariancePercent, absKg, smallPct, mediumPct, smallKg, mediumKg, isZeroWithActual);
            }
            return dto;
        }).ToList();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _stockTakeRepository
            .FindByCondition(x => !x.IsDeleted && x.Id == id)
            .Include(x => x.StockTakeItems)
            .FirstOrDefaultAsync();

        if (data == null)
            return ApiResponse.NotFound();

        var dto = data.ToDto();
        await EnrichVarianceSeverityAsync(dto);

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

        if (obj.StockTakeStatusId == Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Approved) ||
            obj.StockTakeStatusId == Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Rejected))
        {
            return ApiResponse.UnprocessableEntity("Không thể cập nhật trạng thái Duyệt/Từ chối qua chức năng này.", ApiCodeConstants.Common.UnprocessableEntity);
        }

        if (obj.StockTakeStatusId == Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Submitted) &&
            existData.StockTakeStatusId != Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Draft))
        {
            return ApiResponse.UnprocessableEntity("Chỉ có thể Gửi duyệt phiếu đang ở trạng thái Nháp.", ApiCodeConstants.Common.UnprocessableEntity);
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
                RecountConfirmed = itemDto.RecountConfirmed,
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
            .FirstOrDefaultAsync();

        if (existData == null)
            return ApiResponse.NotFound();

        // Tách quyền (segregation of duties): người tạo phiếu không được tự duyệt
        // phiếu kiểm kê do chính mình tạo, kể cả khi vai trò có quyền APPROVE.
        if (existData.CreatedBy.HasValue && existData.CreatedBy.Value == userId)
            return ApiResponse.Forbidden(message: "Bạn không thể tự duyệt phiếu kiểm kê do chính mình tạo.", code: ApiCodeConstants.Common.Forbidden);

        if (existData.StockTakeStatusId != Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Submitted))
            return ApiResponse.UnprocessableEntity("Chỉ có thể duyệt phiếu kiểm kho ở trạng thái chờ duyệt.", ApiCodeConstants.Common.UnprocessableEntity);

        // 3.7 — Chặn duyệt khi phiếu không có dòng kiểm kê
        if (!existData.StockTakeItems.Any())
            return ApiResponse.UnprocessableEntity("Phiếu kiểm kê không có dòng nào. Không thể duyệt phiếu trống.", ApiCodeConstants.Common.UnprocessableEntity);

        // STK-03 — Chặn duyệt khi có dòng thiếu ProductVariantId hoặc LocationId
        var itemsMissingScope = existData.StockTakeItems
            .Where(i => !i.ProductVariantId.HasValue || !i.LocationId.HasValue)
            .ToList();
        if (itemsMissingScope.Any())
        {
            return ApiResponse.UnprocessableEntity(
                $"Phiếu kiểm kê có {itemsMissingScope.Count} dòng thiếu thông tin bắt buộc (Sản phẩm hoặc Vị trí). " +
                "Vui lòng bổ sung trước khi duyệt.",
                ApiCodeConstants.Common.UnprocessableEntity);
        }

        // 3.7 — Chặn duyệt khi có dòng chưa nhập ActualQuantity
        var unfinishedItems = existData.StockTakeItems
            .Where(i => i.ProductVariantId.HasValue && !i.ActualQuantity.HasValue)
            .ToList();
        if (unfinishedItems.Any())
        {
            var names = unfinishedItems
                .Select(i => i.ProductVariant?.Name ?? $"ProductVariantId={i.ProductVariantId}")
                .Take(3);
            return ApiResponse.UnprocessableEntity(
                $"Phiếu còn {unfinishedItems.Count} dòng chưa nhập số lượng kiểm đếm ({string.Join(", ", names)}...). Vui lòng nhập đầy đủ trước khi duyệt.",
                ApiCodeConstants.Common.UnprocessableEntity);
        }

        // --- ĐỌC NGƯỠNG TỪ DB ---
        var (smallPct, mediumPct, smallKg, mediumKg) = await ResolveVarianceThresholdsAsync();

        // --- VALIDATION TỪNG DÒNG TRƯỚC KHI MỞ TRANSACTION ---
        foreach (var item in existData.StockTakeItems)
        {
            if (!item.ActualQuantity.HasValue || !item.ProductVariantId.HasValue)
                continue;

            // Bỏ qua dòng không có chênh lệch (không điều chỉnh tồn)
            if (item.Difference == 0)
                continue;

            var isZeroWithActual = item.SystemQuantity == 0 && (item.ActualQuantity ?? 0) > 0;
            var severity = ClassifySeverity(item.VariancePercent, item.AbsoluteVarianceKg, smallPct, mediumPct, smallKg, mediumKg, isZeroWithActual);
            var variantName = item.ProductVariant?.Name ?? $"ProductVariantId={item.ProductVariantId}";

            // STK-05 — Concurrency check đầy đủ: xử lý cả trường hợp inventory bị xóa (null → 0)
            var currentInventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                item.ProductVariantId.Value, existData.WarehouseId, item.LocationId, item.PaddyLotId);
            var currentQtyOnHand = currentInventory?.QuantityOnHand ?? 0m;
            if (currentQtyOnHand != item.SystemQuantity)
            {
                return ApiResponse.Conflict(
                    $"Tồn kho của '{variantName}' đã thay đổi từ {item.SystemQuantity} thành {currentQtyOnHand} sau khi lập phiếu. " +
                    "Vui lòng tải lại phiếu kiểm kê và cập nhật số liệu trước khi duyệt.");
            }

            // 3.11 — Chặn khi newQuantityOnHand < QuantityReserved
            if (currentInventory != null)
            {
                var newQty = item.ActualQuantity!.Value;
                if (newQty < currentInventory.QuantityReserved)
                {
                    return ApiResponse.UnprocessableEntity(
                        $"Không thể điều chỉnh tồn '{variantName}' xuống {newQty} vì đang có {currentInventory.QuantityReserved} kg đang được đặt giữ (QuantityReserved). " +
                        "Vui lòng giải phóng các đơn đặt hàng liên quan trước khi duyệt.",
                        ApiCodeConstants.Common.UnprocessableEntity);
                }
            }

            // 3.2 — MEDIUM bắt buộc lý do
            if ((severity == "MEDIUM" || severity == "LARGE") && string.IsNullOrWhiteSpace(item.Note))
            {
                return ApiResponse.UnprocessableEntity(
                    $"Dòng kiểm kê '{variantName}' có mức chênh lệch {severity} — bắt buộc phải nhập lý do vào cột Ghi chú trước khi duyệt.",
                    ApiCodeConstants.Common.UnprocessableEntity);
            }

            // 3.3 — LARGE bắt buộc xác nhận kiểm đếm lại
            if (severity == "LARGE" && !item.RecountConfirmed)
            {
                return ApiResponse.UnprocessableEntity(
                    $"Dòng kiểm kê '{variantName}' có mức chênh lệch LARGE — bắt buộc phải xác nhận kiểm đếm lại (RecountConfirmed = true) trước khi duyệt.",
                    ApiCodeConstants.Common.UnprocessableEntity);
            }
        }

        // --- MỞ TRANSACTION VÀ THỰC HIỆN ĐIỀU CHỈNH TỒN KHO ---
        await using var transaction = await _stockTakeRepository.BeginTransactionAsync();
        try
        {
            existData.StockTakeStatusId  = Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Approved);
            existData.ApprovedByUserId   = userId;
            existData.ApproveNote        = approveNote;
            existData.CompletedDate      = DateTimeHelper.VietnamNow();
            existData.LastModifiedDate   = DateTimeHelper.VietnamNow();
            existData.UpdatedBy          = userId;

            var largeItems = new List<(StockTakeItem item, string severity)>();

            foreach (var item in existData.StockTakeItems)
            {
                if (!item.ActualQuantity.HasValue || item.Difference == 0 || !item.ProductVariantId.HasValue)
                    continue;

                var isZeroWithActual = item.SystemQuantity == 0 && (item.ActualQuantity ?? 0) > 0;
                var severity = ClassifySeverity(item.VariancePercent, item.AbsoluteVarianceKg, smallPct, mediumPct, smallKg, mediumKg, isZeroWithActual);

                if (severity == "LARGE") largeItems.Add((item, severity));

                // 3.9 — Ghi item.Note vào InventoryTransaction.Note
                var noteText = $"Điều chỉnh kiểm kho {existData.STCode} — {severity}";
                if (!string.IsNullOrWhiteSpace(item.Note))
                    noteText += $" — Lý do: {item.Note}";

                var request = new DTOs.InventoryTransactions.StockMovementRequestDto
                {
                    ProductVariantId = item.ProductVariantId.Value,
                    WarehouseId      = existData.WarehouseId,
                    LocationId       = item.LocationId,
                    PaddyLotId       = item.PaddyLotId,
                    Quantity         = Math.Abs(item.Difference),
                    ReferenceType    = "STOCKTAKE",
                    ReferenceId      = existData.Id,
                    ReferenceItemId  = item.Id,
                    Note             = noteText
                };

                var result = await _inventoryTransactionService.AdjustStockAsync(request, item.ActualQuantity.Value, true);
                if (!result.IsSucceeded)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse.UnprocessableEntity($"Lỗi điều chỉnh tồn kho: {result.Message}", ApiCodeConstants.Common.UnprocessableEntity);
                }
            }

            await _stockTakeRepository.UpdateAsync(existData);
            await _stockTakeRepository.SaveChangesAsync();

            // 3.4 — Tạo Alert CRITICAL cho từng dòng LARGE, trong cùng transaction
            foreach (var (item, severity) in largeItems)
            {
                var lotInfo = item.PaddyLotId.HasValue ? $" (Lô #{item.PaddyLotId})" : "";
                var alert = new Alert
                {
                    AlertType          = "STOCK_TAKE_LARGE_VARIANCE",
                    Severity           = "CRITICAL",
                    WarehouseId        = existData.WarehouseId,
                    ProductVariantId   = item.ProductVariantId,
                    LocationId         = item.LocationId,
                    Message            = $"[{existData.STCode}] Chênh lệch LARGE: {item.ProductVariant?.Name ?? $"PV#{item.ProductVariantId}"}{lotInfo} — " +
                                         $"Hệ thống: {item.SystemQuantity:F2} kg, Thực tế: {item.ActualQuantity:F2} kg, " +
                                         $"Chênh: {item.AbsoluteVarianceKg:F2} kg ({item.VariancePercent:F2}%). " +
                                         $"Lý do: {item.Note ?? "(chưa ghi)"}",
                    RelatedEntityType  = "StockTakeItem",
                    RelatedEntityId    = item.Id,
                    Status             = "OPEN",
                    DeduplicationKey   = $"STOCKTAKE_LARGE_{existData.Id}_{item.Id}",
                    CreatedDate        = DateTimeHelper.VietnamNow(),
                    CreatedBy          = userId
                };
                await _context.Alerts.AddAsync(alert);
            }

            if (largeItems.Any())
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
