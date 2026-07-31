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

    public StockTakeService(
        IStockTakeRepository stockTakeRepository,
        IStockTakeItemRepository stockTakeItemRepository,
        IInventoryTransactionService inventoryTransactionService,
        IInventoryRepository inventoryRepository,
        ISystemConfigRepository systemConfigRepository,
        Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
        INotificationDispatcher notificationDispatcher)
    {
        _stockTakeRepository = stockTakeRepository;
        _stockTakeItemRepository = stockTakeItemRepository;
        _inventoryTransactionService = inventoryTransactionService;
        _inventoryRepository = inventoryRepository;
        _systemConfigRepository = systemConfigRepository;
        _httpContextAccessor = httpContextAccessor;
        _notificationDispatcher = notificationDispatcher;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Đọc ngưỡng phân loại chênh lệch từ SystemConfig.
    /// Fallback về Defaults nếu chưa cấu hình hoặc giá trị không hợp lệ.
    /// </summary>
    private async Task<(decimal small, decimal medium)> ResolveVarianceThresholdsAsync()
    {
        decimal small = SystemConfigConstants.Defaults.StockTakeSmallVariancePercent;
        decimal medium = SystemConfigConstants.Defaults.StockTakeMediumVariancePercent;

        var smallRaw = await _systemConfigRepository.GetValueByKey(SystemConfigConstants.Keys.StockTakeSmallVariancePercent);
        if (decimal.TryParse(smallRaw, out var parsedSmall) && parsedSmall > 0)
            small = parsedSmall;

        var mediumRaw = await _systemConfigRepository.GetValueByKey(SystemConfigConstants.Keys.StockTakeMediumVariancePercent);
        if (decimal.TryParse(mediumRaw, out var parsedMedium) && parsedMedium > small)
            medium = parsedMedium;

        return (small, medium);
    }

    /// <summary>
    /// Tính VarianceSeverity cho một dòng kiểm kê dựa trên ngưỡng từ DB.
    /// </summary>
    private static string ClassifySeverity(decimal? variancePercent, decimal smallThreshold, decimal mediumThreshold)
    {
        if (variancePercent == null || variancePercent == 0)
            return "NONE";
        if (variancePercent <= smallThreshold)
            return "SMALL";
        if (variancePercent <= mediumThreshold)
            return "MEDIUM";
        return "LARGE";
    }

    /// <summary>
    /// Enrich VarianceSeverity trên tất cả dòng của DTO sau khi đọc ngưỡng từ DB.
    /// </summary>
    private async Task EnrichVarianceSeverityAsync(StockTakeDto dto)
    {
        var (small, medium) = await ResolveVarianceThresholdsAsync();
        foreach (var item in dto.StockTakeItems)
        {
            item.VarianceSeverity = ClassifySeverity(item.VariancePercent, small, medium);
        }
    }

    // ─── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<ApiResponse> CreateAsync(CreateStockTakeDto obj)
    {
        var model = obj.ToEntity();
        model.StartedDate = DateTimeHelper.VietnamNow();

        foreach (var item in model.StockTakeItems)
        {
            if (item.ProductVariantId.HasValue)
            {
                // Tính SystemQuantity: nếu chỉ định lô → lấy tồn của lô đó; không lô → tổng tất cả lô tại vị trí.
                item.SystemQuantity = await _inventoryRepository
                    .FindByCondition(x =>
                        !x.IsDeleted &&
                        x.ProductVariantId == item.ProductVariantId.Value &&
                        x.WarehouseId == model.WarehouseId &&
                        x.LocationId == item.LocationId &&
                        (!item.PaddyLotId.HasValue || x.PaddyLotId == item.PaddyLotId))
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
        // Không dùng .Select(x => x.ToDto()).ToListAsync() trực tiếp trên IQueryable vì
        // EF Core 3+ không tự fallback về client-side khi gặp method call không translate được.
        var entities = await _stockTakeRepository
            .FindByCondition(x => !x.IsDeleted)
            .Include(x => x.StockTakeItems)
            .ToListAsync();

        var (small, medium) = await ResolveVarianceThresholdsAsync();

        var data = entities.Select(e =>
        {
            var dto = e.ToDto();
            foreach (var item in dto.StockTakeItems)
                item.VarianceSeverity = ClassifySeverity(item.VariancePercent, small, medium);
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

        obj.ToEntity(existData);

        // 1. Xóa dòng không còn trong DTO
        var dtoItemIds = obj.StockTakeItems.Where(i => i.Id > 0).Select(i => i.Id).ToList();
        var itemsToDelete = existData.StockTakeItems.Where(i => !dtoItemIds.Contains(i.Id)).ToList();
        foreach (var item in itemsToDelete)
            await _stockTakeItemRepository.HardDeleteAsync(item.Id);

        // 2. Cập nhật dòng hiện có
        var now = DateTimeHelper.VietnamNow();
        foreach (var itemDto in obj.StockTakeItems.Where(i => i.Id > 0))
        {
            var existingItem = existData.StockTakeItems.FirstOrDefault(i => i.Id == itemDto.Id);
            if (existingItem != null)
            {
                existingItem.ProductVariantId = itemDto.ProductVariantId;
                existingItem.LocationId = itemDto.LocationId;
                existingItem.PaddyLotId = itemDto.PaddyLotId;
                existingItem.ActualQuantity = itemDto.ActualQuantity;
                existingItem.Note = itemDto.Note;
                existingItem.QRScanned = itemDto.QRScanned;
                existingItem.LastModifiedDate = now;
            }
        }

        // 3. Thêm dòng mới
        var newItems = new List<StockTakeItem>();
        foreach (var itemDto in obj.StockTakeItems.Where(i => i.Id == 0))
        {
            decimal sysQty = 0;
            if (itemDto.ProductVariantId.HasValue)
            {
                sysQty = await _inventoryRepository
                    .FindByCondition(x =>
                        !x.IsDeleted &&
                        x.ProductVariantId == itemDto.ProductVariantId.Value &&
                        x.WarehouseId == existData.WarehouseId &&
                        x.LocationId == itemDto.LocationId &&
                        (!itemDto.PaddyLotId.HasValue || x.PaddyLotId == itemDto.PaddyLotId))
                    .SumAsync(x => (decimal?)x.QuantityOnHand) ?? 0m;
            }

            newItems.Add(new StockTakeItem
            {
                StockTakeId = existData.Id,
                ProductVariantId = itemDto.ProductVariantId,
                LocationId = itemDto.LocationId,
                PaddyLotId = itemDto.PaddyLotId,
                SystemQuantity = sysQty,
                ActualQuantity = itemDto.ActualQuantity,
                Note = itemDto.Note,
                QRScanned = itemDto.QRScanned,
                CreatedDate = now
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
        var currentRoleIds = _httpContextAccessor.HttpContext?.GetCurrentRoleIds() ?? new List<int>();
        if (!currentRoleIds.Contains(CommonConstants.Role.ADMIN) && !currentRoleIds.Contains(CommonConstants.Role.OWNER))
            return ApiResponse.Forbidden();

        var existData = await _stockTakeRepository
            .FindByCondition(x => !x.IsDeleted && x.Id == id)
            .Include(x => x.StockTakeItems)
                .ThenInclude(i => i.ProductVariant)
            .FirstOrDefaultAsync();

        if (existData == null)
            return ApiResponse.NotFound();

        if (existData.StockTakeStatusId != Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Submitted))
            return ApiResponse.UnprocessableEntity("Chỉ có thể duyệt phiếu kiểm kho ở trạng thái chờ duyệt.", ApiCodeConstants.Common.UnprocessableEntity);

        // --- BƯỚC 1: ĐỌC NGƯỠNG TỪ DB (không hard-code) ---
        var (smallThreshold, mediumThreshold) = await ResolveVarianceThresholdsAsync();

        // --- BƯỚC 2: VALIDATION TRƯỚC KHI MỞ TRANSACTION ---
        // Kiểm tra toàn bộ dòng trước, tránh phải revert entity state thủ công sau rollback.
        foreach (var item in existData.StockTakeItems)
        {
            if (!item.ActualQuantity.HasValue || item.Difference == 0 || !item.ProductVariantId.HasValue)
                continue;

            var severity = ClassifySeverity(item.VariancePercent, smallThreshold, mediumThreshold);

            // Bắt buộc ghi lý do với chênh lệch mức LARGE
            if (severity == "LARGE" && string.IsNullOrWhiteSpace(item.Note))
            {
                var variantName = item.ProductVariant?.Name ?? $"ProductVariantId={item.ProductVariantId}";
                return ApiResponse.UnprocessableEntity(
                    $"Dòng kiểm kê '{variantName}' có chênh lệch {item.VariancePercent:F2}% (mức LARGE > {mediumThreshold}%) — bắt buộc phải nhập lý do vào cột Ghi chú trước khi duyệt.",
                    ApiCodeConstants.Common.UnprocessableEntity);
            }
        }

        // --- BƯỚC 3: MỞ TRANSACTION VÀ THỰC HIỆN ĐIỀU CHỈNH TỒN KHO ---
        await using var transaction = await _stockTakeRepository.BeginTransactionAsync();
        try
        {
            existData.StockTakeStatusId = Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Approved);
            existData.ApprovedByUserId = userId;
            existData.ApproveNote = approveNote;
            existData.CompletedDate = DateTimeHelper.VietnamNow();
            existData.LastModifiedDate = DateTimeHelper.VietnamNow();
            existData.UpdatedBy = userId;

            foreach (var item in existData.StockTakeItems)
            {
                if (!item.ActualQuantity.HasValue || item.Difference == 0 || !item.ProductVariantId.HasValue)
                    continue;

                // Truyền PaddyLotId vào request để AdjustStockAsync khớp đúng dòng tồn kho theo lô.
                // Nếu PaddyLotId = null (hàng không theo lô), AdjustStockAsync xử lý theo variant+location.
                var request = new DTOs.InventoryTransactions.StockMovementRequestDto
                {
                    ProductVariantId = item.ProductVariantId.Value,
                    WarehouseId = existData.WarehouseId,
                    LocationId = item.LocationId,
                    PaddyLotId = item.PaddyLotId,
                    Quantity = Math.Abs(item.Difference),
                    ReferenceType = "STOCKTAKE",
                    ReferenceId = existData.Id,
                    ReferenceItemId = item.Id,
                    Note = $"Điều chỉnh kiểm kho {existData.STCode} — {ClassifySeverity(item.VariancePercent, smallThreshold, mediumThreshold)}"
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
            return ApiResponse.Forbidden();

        var existData = await _stockTakeRepository.FindByCondition(x => !x.IsDeleted && x.Id == id).FirstOrDefaultAsync();
        if (existData == null)
            return ApiResponse.NotFound();

        if (existData.StockTakeStatusId != Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Submitted))
            return ApiResponse.UnprocessableEntity("Chỉ có thể từ chối phiếu kiểm kho ở trạng thái chờ duyệt.", ApiCodeConstants.Common.UnprocessableEntity);

        await using var transaction = await _stockTakeRepository.BeginTransactionAsync();
        try
        {
            existData.StockTakeStatusId = Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Rejected);
            existData.Note = string.IsNullOrWhiteSpace(existData.Note) ? $"Từ chối: {reason}" : $"{existData.Note} | Từ chối: {reason}";
            existData.LastModifiedDate = DateTimeHelper.VietnamNow();
            existData.UpdatedBy = userId;

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

