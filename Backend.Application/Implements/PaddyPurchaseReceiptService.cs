using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Backend.Application.BackgroundJobs.DebtDueOverdue;
using Backend.Share.Services;
using Backend.Application.Constants;
using Backend.Application.DTOs.PaddyPurchaseReceipts;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Backend.Share.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ phiếu mua lúa.
/// ConfirmReceiptAsync: chốt phiếu → sinh lô, tạo InboundOrder Draft và ghi công nợ.
/// </summary>
public class PaddyPurchaseReceiptService : IPaddyPurchaseReceiptService
{
    private readonly IPaddyPurchaseReceiptRepository _receiptRepository;
    private readonly IPaddyLotRepository _paddyLotRepository;
    private readonly IRepositoryBase<InboundOrder, int> _inboundOrderRepository;
    private readonly IRepositoryBase<InboundOrderItem, int> _inboundOrderItemRepository;
    private readonly IRepositoryBase<InboundOrderStatus, int> _inboundOrderStatusRepository;
    private readonly IRepositoryBase<LotStatus, int> _lotStatusRepository;
    private readonly IRepositoryBase<QualityInspection, int> _qualityInspectionRepository;
    private readonly IRepositoryBase<PartyDebt, int> _partyDebtRepository;
    private readonly IRepositoryBase<DebtTransaction, int> _debtTransactionRepository;
    private readonly IProductVariantRepository _productVariantRepository;
    private readonly IPaddyPurchaseScheduleRepository _scheduleRepository;
    private readonly ISystemLookup _systemLookup;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryTransactionRepository _inventoryTransactionRepository;
    private readonly IRepositoryBase<PutawayDecision, long> _putawayDecisionRepository;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly IScheduledJobService? _scheduledJobService;
    private readonly IRepositoryBase<PaddyLotBag, int>? _paddyLotBagRepository;

    public PaddyPurchaseReceiptService(
        IPaddyPurchaseReceiptRepository receiptRepository,
        IPaddyLotRepository paddyLotRepository,
        IRepositoryBase<InboundOrder, int> inboundOrderRepository,
        IRepositoryBase<InboundOrderItem, int> inboundOrderItemRepository,
        IRepositoryBase<InboundOrderStatus, int> inboundOrderStatusRepository,
        IRepositoryBase<LotStatus, int> lotStatusRepository,
        IRepositoryBase<QualityInspection, int> qualityInspectionRepository,
        IRepositoryBase<PartyDebt, int> partyDebtRepository,
        IRepositoryBase<DebtTransaction, int> debtTransactionRepository,
        IProductVariantRepository productVariantRepository,
        IPaddyPurchaseScheduleRepository scheduleRepository,
        ISystemLookup systemLookup,
        IHttpContextAccessor httpContextAccessor,
        IInventoryRepository inventoryRepository,
        IInventoryTransactionRepository inventoryTransactionRepository,
        IRepositoryBase<PutawayDecision, long> putawayDecisionRepository,
        INotificationDispatcher notificationDispatcher,
        IScheduledJobService? scheduledJobService = null,
        IRepositoryBase<PaddyLotBag, int>? paddyLotBagRepository = null)
    {
        _receiptRepository = receiptRepository;
        _paddyLotRepository = paddyLotRepository;
        _inboundOrderRepository = inboundOrderRepository;
        _inboundOrderItemRepository = inboundOrderItemRepository;
        _inboundOrderStatusRepository = inboundOrderStatusRepository;
        _lotStatusRepository = lotStatusRepository;
        _qualityInspectionRepository = qualityInspectionRepository;
        _partyDebtRepository = partyDebtRepository;
        _debtTransactionRepository = debtTransactionRepository;
        _productVariantRepository = productVariantRepository;
        _scheduleRepository = scheduleRepository;
        _systemLookup = systemLookup;
        _httpContextAccessor = httpContextAccessor;
        _inventoryRepository = inventoryRepository;
        _inventoryTransactionRepository = inventoryTransactionRepository;
        _putawayDecisionRepository = putawayDecisionRepository;
        _notificationDispatcher = notificationDispatcher;
        _scheduledJobService = scheduledJobService;
        _paddyLotBagRepository = paddyLotBagRepository;
    }

    private int GetCurrentUserId()
        => _httpContextAccessor.HttpContext?.GetCurrentUserId() ?? 1;

    public async Task<ApiResponse> CreateAsync(CreatePaddyPurchaseReceiptDto obj)
    {
        NormalizeBagTrace(obj.Bags, GetCurrentUserId());
        NormalizeReceiptAmounts(obj);
        if (obj.PaidAmount < 0 || obj.PaidAmount > obj.TotalAmount)
            return ApiResponse.UnprocessableEntity("Số tiền đã trả phải nằm trong khoảng từ 0 đến tổng tiền.");
        var bagError = ValidateBags(obj.Bags, obj.ActualWeightKg);
        if (bagError != null) return ApiResponse.UnprocessableEntity(bagError);

        // Chặn lập phiếu trùng: lịch đã đủ khối lượng dự kiến (hoặc đã có phiếu khi lịch không khai báo dự kiến).
        var scheduleError = await ValidateScheduleCapacityAsync(obj.ScheduleId, excludeReceiptId: null);
        if (scheduleError != null) return ApiResponse.UnprocessableEntity(scheduleError);

        var datePart = DateTimeHelper.VietnamNow().ToString("yyyyMMdd");
        var baseCode = $"PPR-{datePart}";
        var count = await _receiptRepository.FindByCondition(x => x.ReceiptCode.StartsWith(baseCode)).CountAsync();
        var receiptCode = $"{baseCode}-{(count + 1):D4}";

        // #5: Retry chống trùng mã khi nhiều request chạy đồng thời
        int codeAttempts = 0;
        while (await _receiptRepository.FindByCondition(x => x.ReceiptCode == receiptCode).AnyAsync() && codeAttempts < 10)
        {
            codeAttempts++;
            receiptCode = $"{baseCode}-{(count + 1 + codeAttempts):D4}";
        }

        var model = obj.ToEntity(receiptCode);
        await _receiptRepository.CreateAsync(model);
        await _receiptRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id, "Tạo phiếu mua lúa thành công.");
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreatePaddyPurchaseReceiptDto> objs)
        => Task.FromResult(ApiResponse.Error(message: "CreateList chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> GetAllAsync()
    {
        // Include đủ Lô + Lịch để isConfirmed / scheduleCode trả về đúng (mobile phân loại phiếu nháp dựa vào đây).
        var entities = await _receiptRepository
            .FindByCondition(x => !x.IsDeleted, false,
                x => x.Farmer, x => x.Warehouse, x => x.RiceVariety, x => x.ProductVariant,
                x => x.PaddyLot, x => x.Schedule)
            .OrderByDescending(x => x.ReceiptDate)
            .ToListAsync();

        return ApiResponse.Success(entities.Select(x => x.ToDto()).ToList());
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var entity = await _receiptRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted,
                false,
                x => x.Farmer,
                x => x.Warehouse,
                x => x.RiceVariety,
                x => x.ProductVariant,
                x => x.PaddyLot,
                x => x.Schedule)
            .FirstOrDefaultAsync();

        if (entity == null) return ApiResponse.NotFound();
        return ApiResponse.Success(entity.ToDto());
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _receiptRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query)
        => Task.FromResult(ApiResponse.Error(message: "GetPaged (SearchQuery) chưa được hỗ trợ.", status: 501));
    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
        => Task.FromResult(ApiResponse.Error(message: "GetPaged (AdvancedSearchQuery) chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> UpdateAsync(UpdatePaddyPurchaseReceiptDto obj)
    {
        NormalizeBagTrace(obj.Bags, GetCurrentUserId());
        NormalizeReceiptAmounts(obj);
        if (obj.PaidAmount < 0 || obj.PaidAmount > obj.TotalAmount)
            return ApiResponse.UnprocessableEntity("Số tiền đã trả phải nằm trong khoảng từ 0 đến tổng tiền.");
        var bagError = ValidateBags(obj.Bags, obj.ActualWeightKg);
        if (bagError != null) return ApiResponse.UnprocessableEntity(bagError);
        var existData = await _receiptRepository.GetByIdAsync(obj.Id);
        if (existData == null || existData.IsDeleted) return ApiResponse.NotFound();

        // Không cho sửa phiếu đã chốt
        var isConfirmed = await _paddyLotRepository.AnyAsync(x => x.SourceReceiptId == obj.Id && !x.IsDeleted);
        if (isConfirmed)
            return ApiResponse.UnprocessableEntity("Phiếu đã được chốt, không thể chỉnh sửa.");

        // Đổi lịch / đổi khối lượng cũng phải nằm trong hạn mức của lịch (bỏ qua chính phiếu đang sửa).
        var scheduleError = await ValidateScheduleCapacityAsync(obj.ScheduleId, excludeReceiptId: obj.Id);
        if (scheduleError != null) return ApiResponse.UnprocessableEntity(scheduleError);

        obj.ToEntity(existData);
        await _receiptRepository.UpdateAsync(existData);
        await _receiptRepository.SaveChangesAsync();

        return ApiResponse.Success(existData.Id, "Cập nhật phiếu mua lúa thành công.");
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdatePaddyPurchaseReceiptDto> objs)
        => Task.FromResult(ApiResponse.Error(message: "UpdateList chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var receipt = await _receiptRepository.GetByIdAsync(id);
        if (receipt == null) return ApiResponse.NotFound();

        var isDeleted = await _receiptRepository.SoftDeleteAsync(id);
        if (!isDeleted) return ApiResponse.BadRequest();
        await _receiptRepository.SaveChangesAsync();

        if (receipt.ScheduleId.HasValue)
        {
            await UpdateScheduleStatusAsync(receipt.ScheduleId.Value, GetCurrentUserId());
        }

        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        // Lấy danh sách scheduleIds của các phiếu sắp bị xóa để tính toán lại trạng thái sau khi xóa
        var scheduleIds = await _receiptRepository
            .FindByCondition(x => objs.Contains(x.Id) && x.ScheduleId != null && !x.IsDeleted)
            .Select(x => x.ScheduleId!.Value)
            .Distinct()
            .ToListAsync();

        var isDeleted = await _receiptRepository.SoftDeleteListAsync(objs);
        if (!isDeleted) return ApiResponse.BadRequest();
        await _receiptRepository.SaveChangesAsync();

        foreach (var scheduleId in scheduleIds)
        {
            await UpdateScheduleStatusAsync(scheduleId, GetCurrentUserId());
        }

        return ApiResponse.Success(isDeleted);
    }

    /// <summary>
    /// Chốt phiếu: sinh PaddyLot, tạo InboundOrder Draft + Item và ghi công nợ.
    /// Tồn kho chỉ tăng khi Inbound xác nhận Put-away.
    /// </summary>
    public async Task<ApiResponse> ConfirmReceiptAsync(int receiptId, ConfirmPaddyPurchaseReceiptDto dto, int confirmedById)
    {
        // 1. Load phiếu
        var receipt = await _receiptRepository
            .FindByCondition(x => x.Id == receiptId && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (receipt == null) return ApiResponse.NotFound();

        // 1. Kiểm tra trạng thái chốt phiếu: Nếu đã được chốt (đã tồn tại PaddyLot liên kết) thì không cho chốt lại
        var alreadyConfirmed = await _paddyLotRepository.AnyAsync(x => x.SourceReceiptId == receiptId && !x.IsDeleted);
        if (alreadyConfirmed)
            return ApiResponse.UnprocessableEntity("Phiếu này đã được chốt trước đó.");

        var now = DateTimeHelper.VietnamNow();
        if (receipt.DebtAmount > 0)
        {
            if (!dto.DueDate.HasValue)
                return ApiResponse.UnprocessableEntity("Vui lòng chọn hạn thanh toán trước khi chốt phiếu có phát sinh công nợ.");
            if (dto.DueDate.Value.Date < now.Date)
                return ApiResponse.UnprocessableEntity("Hạn thanh toán không được trước ngày hiện tại.");
        }

        // W14-G: Lưu DebtDueDate tạm — công nợ chính thức chỉ được ghi sau khi Receiving QC hoàn tất
        // để tính đúng trên trọng lượng thực nhận (không bao gồm bao bị trả về).
        if (dto.DueDate.HasValue)
        {
            receipt.DebtDueDate = dto.DueDate.Value.Date;
            await _receiptRepository.UpdateAsync(receipt);
            await _receiptRepository.SaveChangesAsync();
        }

        // 2. Tự động sinh mã lô hàng (LotCode): LOT-PADDY-YYYYMMDD-XXXX
        var datePart = now.ToString("yyyyMMdd");
        var baseCode = $"LOT-PADDY-{datePart}";
        var count = await _paddyLotRepository.FindByCondition(x => x.LotCode.StartsWith(baseCode)).CountAsync();
        var lotCode = $"{baseCode}-{(count + 1):D4}";

        // #5: Retry chống trùng mã lô khi nhiều request chốt phiếu đồng thời
        int lotCodeAttempts = 0;
        while (await _paddyLotRepository.FindByCondition(x => x.LotCode == lotCode).AnyAsync() && lotCodeAttempts < 10)
        {
            lotCodeAttempts++;
            lotCode = $"{baseCode}-{(count + 1 + lotCodeAttempts):D4}";
        }

        // 3. Trạng thái lô khởi tạo: CHỜ KIỂM ĐỊNH (AWAITING_QC) — lô chỉ hiện ở màn Chất lượng
        //    & cách ly, chưa hiện ở màn Nhập kho cho tới khi kiểm định xong. Nếu quản trị chưa
        //    thêm trạng thái AWAITING_QC (thêm thủ công qua web) thì fallback về "Chờ nhập".
        var defaultLotStatus =
            await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == LotStatusCodeConstants.AwaitingQc && !x.IsDeleted)
            ?? await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == LotStatusCodeConstants.PendingInbound && !x.IsDeleted)
            ?? await _lotStatusRepository.FirstOrDefaultAsync(x => !x.IsDeleted)
            ?? throw new InvalidOperationException("Không tìm thấy LotStatus nào trong hệ thống.");

        // Bọc toàn bộ trong DB Transaction đảm bảo tính nguyên tử (Atomic)
        await using var tx = await _receiptRepository.BeginTransactionAsync();
        try
        {
            // Xác định Id của Variant sản phẩm: ưu tiên biến thể người dùng đã chọn trên phiếu,
            // nếu phiếu chưa có (dữ liệu cũ) thì suy ra mặc định theo giống lúa.
            var productVariantId = await ResolvePaddyVariantIdAsync(receipt);

            // Lô đã được định danh nhưng chưa nằm trong tồn kho vật lý.
            var lot = new PaddyLot
            {
                OrganizationId = receipt.OrganizationId,
                LotCode = lotCode,
                LotType = "PADDY",
                ProductVariantId = productVariantId,
                RiceVarietyId = receipt.RiceVarietyId,
                StatusId = defaultLotStatus.Id,
                SourceReceiptId = receiptId,
                WarehouseId = receipt.WarehouseId,
                InboundDate = receipt.ReceiptDate,
                InitialWeightKg = receipt.ActualWeightKg,
                RemainingWeightKg = 0m,
                CostPricePerKg = receipt.ActualWeightKg > 0 ? receipt.TotalAmount / receipt.ActualWeightKg : 0,
                QualityStatus = MapQualityStatus(receipt.QualityJson),
                QrCode = "PL-" + Guid.NewGuid().ToString("N").ToUpper(),
                CreatedBy = confirmedById,
                CreatedDate = now
            };

            await _paddyLotRepository.CreateAsync(lot);
            await _paddyLotRepository.SaveChangesAsync();

            var bags = string.IsNullOrWhiteSpace(receipt.BagDetailsJson)
                ? new List<DTOs.InboundOrders.CreateBagDto>()
                : JsonSerializer.Deserialize<List<DTOs.InboundOrders.CreateBagDto>>(receipt.BagDetailsJson) ?? new();
            if (bags.Count > 0)
            {
                if (_paddyLotBagRepository == null)
                    throw new InvalidOperationException("Dịch vụ lưu chi tiết bao chưa được cấu hình.");
                foreach (var bag in bags)
                {
                    await _paddyLotBagRepository.CreateAsync(new PaddyLotBag
                    {
                        LotId = lot.Id, BagNo = bag.BagNo, WeightKg = bag.WeightKg,
                        Status = "Pending", QrCode = $"PLB-{Guid.NewGuid():N}".ToUpperInvariant(),
                        BagKind = "Purchase", IsFull = true,
                        // W14-J: Copy thông tin truy vết cân từ CreateBagDto
                        ScaleDeviceRef      = bag.ScaleDeviceRef?.Trim(),
                        WeightCaptureMethod = bag.WeightCaptureMethod?.Trim(),
                        WeighedAt           = bag.WeighedAt,
                        WeighedBy           = bag.WeighedBy,
                        Contents = new List<PaddyLotBagContent>
                        {
                            new() { LotId = lot.Id, WeightKg = bag.WeightKg, CreatedBy = confirmedById, CreatedDate = now }
                        },
                        CreatedBy = confirmedById, CreatedDate = now
                    });
                }
                await _paddyLotBagRepository.SaveChangesAsync();
            }

            // 5. TẠO PHIẾU KIỂM ĐỊNH CHẤT LƯỢNG (nháp) — chờ nhập kết quả.
            //    KHÔNG tạo phiếu nhập kho ở bước này. Phiếu nhập kho chỉ được tạo SAU khi
            //    kiểm định (Duyệt đạt / Cách ly) tại màn Chất lượng & cách ly.
            var draftInspection = new QualityInspection
            {
                PaddyLotId     = lot.Id,
                InspectorId    = null,
                InspectionType = InspectionTypeConstants.Receiving,
                InspectedAt    = now,
                PassedInspection = false, // chưa quyết định — phân biệt bằng trạng thái lô AWAITING_QC
                Note = $"Phiếu kiểm định (chờ nhập kết quả) — lô {lot.LotCode} từ phiếu mua {receipt.ReceiptCode}",
                CreatedBy      = confirmedById,
                CreatedDate    = now
            };
            await _qualityInspectionRepository.CreateAsync(draftInspection);
            await _qualityInspectionRepository.SaveChangesAsync();


            // 6. CÔNG NỢ — W14-G: KHÔNG ghi công nợ tại đây.
            //    Debt sẽ được tính và post sau khi Receiving QC hoàn tất (FinalizeFinanceAfterQcAsync),
            //    dựa trên trọng lượng thực nhận (loại trừ bao bị REJECT_RETURN).
            //    DebtDueDate đã được lưu ở bước validate ở trên.

            // 7. Lịch chỉ chuyển sang WEIGHED; STOCKED chỉ sau khi Inbound xác nhận đủ.
            if (receipt.ScheduleId.HasValue)
            {
                await UpdateScheduleStatusAsync(receipt.ScheduleId.Value, confirmedById);
            }

            await _receiptRepository.EndTransactionAsync();

            // Thông báo cho Chủ kho và Nhân viên kho: phiếu mua lúa đã chốt, đã sinh lô + phiếu kiểm định.
            await _notificationDispatcher.DispatchAsync(
                NotificationConstants.Code.PurchaseReceiptConfirmed,
                new NotificationTarget { RoleIds = new List<int> { CommonConstants.Role.OWNER, CommonConstants.Role.WAREHOUSE } },
                new object[] { receipt.ReceiptCode },
                "/admin/quality-inspections",
                confirmedById);

            return ApiResponse.Success(new { LotId = lot.Id, LotCode = lot.LotCode, QualityInspectionId = draftInspection.Id },
                "Chốt phiếu thành công. Đã sinh lô và phiếu kiểm định chất lượng (chờ kiểm định).");
        }
        catch (InvalidOperationException ex)
        {
            await _receiptRepository.RollbackTransactionAsync();
            return ApiResponse.UnprocessableEntity(ex.Message);
        }
        catch (DbUpdateException dbEx) when (
            dbEx.InnerException != null && (
                dbEx.InnerException.Message.Contains("Duplicate entry") ||
                dbEx.InnerException.Message.Contains("1062") ||
                dbEx.InnerException.Message.Contains("IX_PaddyLot_SourceReceiptId")))
        {
            // #6: Hai request chốt cùng lúc — request thứ 2 vi phạm unique index SourceReceiptId.
            // Coi như idempotent: phiếu đã được chốt trước đó.
            await _receiptRepository.RollbackTransactionAsync();
            return ApiResponse.UnprocessableEntity("Phiếu này đã được chốt trước đó.");
        }
        catch
        {
            await _receiptRepository.RollbackTransactionAsync();
            throw;
        }
    }

    /// <summary>
    /// Danh sách biến thể lúa (không phải phụ phẩm) đang hoạt động để chọn trên phiếu mua.
    /// FE lọc tiếp theo giống lúa (RiceVarietyId) của phiếu.
    /// </summary>
    public async Task<ApiResponse> GetProductVariantLookupAsync()
    {
        var items = await _productVariantRepository
            .FindByCondition(x => !x.IsDeleted
                && x.IsActive
                && !x.IsByproduct
                && x.Product.ProductCategoryId == CommonConstants.ProductCategory.Paddy)
            .Select(x => new PaddyProductVariantLookupDto
            {
                Id = x.Id,
                Name = x.Name,
                Sku = x.SKU,
                RiceVarietyId = x.RiceVarietyId,
                RiceVarietyName = x.RiceVariety == null ? null : x.RiceVariety.Name,
                IsActive = x.IsActive
            })
            .ToListAsync();

        return ApiResponse.Success(items);
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Chặn lập phiếu mua trùng trên cùng một lịch thu mua.
    /// Lịch đã đủ khối lượng dự kiến (tổng khối lượng các phiếu chưa xóa >= EstimatedQtyKg)
    /// thì không cho lập thêm phiếu. Lịch không khai báo khối lượng dự kiến chỉ được 1 phiếu.
    /// Ngoài ra chặn theo trạng thái lịch (đã hủy / đã nhập kho / nhập kho một phần).
    /// </summary>
    /// <returns>Thông báo lỗi, hoặc null khi hợp lệ.</returns>
    private async Task<string?> ValidateScheduleCapacityAsync(int? scheduleId, int? excludeReceiptId)
    {
        if (scheduleId is not > 0) return null;

        var schedule = await _scheduleRepository
            .FindByCondition(x => x.Id == scheduleId.Value && !x.IsDeleted, false, x => x.Status)
            .FirstOrDefaultAsync();

        if (schedule == null) return "Lịch thu mua không tồn tại hoặc đã bị xóa.";

        var statusCode = schedule.Status?.Code;
        if (PaddyScheduleReceiptRule.IsBlockedByStatus(statusCode))
        {
            return statusCode?.ToUpperInvariant() == "CANCELLED"
                ? $"Lịch thu mua {schedule.ScheduleCode} đã hủy, không thể lập phiếu mua."
                : $"Lịch thu mua {schedule.ScheduleCode} đã nhập kho, không thể lập thêm phiếu mua.";
        }

        // Id luôn > 0 nên dùng 0 làm "không loại trừ phiếu nào" — giữ truy vấn đơn giản, dịch được sang SQL.
        var excludeId = excludeReceiptId ?? 0;
        var existing = await _receiptRepository
            .FindByCondition(x => x.ScheduleId == scheduleId.Value
                && !x.IsDeleted
                && x.Id != excludeId)
            .Select(x => new { x.ActualWeightKg })
            .ToListAsync();

        var receiptedWeightKg = existing.Sum(x => x.ActualWeightKg);
        var receiptCount = existing.Count;

        if (PaddyScheduleReceiptRule.IsFullyReceipted(schedule.EstimatedQtyKg, receiptedWeightKg, receiptCount))
        {
            return schedule.EstimatedQtyKg is > 0
                ? $"Lịch thu mua {schedule.ScheduleCode} đã có {receiptCount} phiếu mua với tổng {receiptedWeightKg:0.###} kg, "
                  + $"đủ khối lượng dự kiến ({schedule.EstimatedQtyKg:0.###} kg). Không thể lập thêm phiếu."
                : $"Lịch thu mua {schedule.ScheduleCode} đã có phiếu mua. Không thể lập thêm phiếu cho lịch này.";
        }

        // Cân thực tế được phép vượt khối lượng dự kiến của lịch — chỉ chặn khi lịch ĐÃ đủ từ trước.
        return null;
    }

    /// <summary>
    /// Xác định biến thể sản phẩm cho lô sinh ra từ phiếu mua.
    /// Ưu tiên biến thể người dùng đã chọn trên phiếu (ProductVariantId); nếu hợp lệ thì dùng luôn.
    /// Nếu phiếu chưa chọn (dữ liệu cũ) hoặc biến thể không còn hợp lệ thì suy ra mặc định theo giống lúa.
    /// </summary>
    private async Task<int> ResolvePaddyVariantIdAsync(PaddyPurchaseReceipt receipt)
    {
        if (receipt.ProductVariantId.HasValue)
        {
            var chosen = await _productVariantRepository
                .FindByCondition(x => x.Id == receipt.ProductVariantId.Value
                    && !x.IsDeleted
                    && x.IsActive
                    && !x.IsByproduct
                    && x.Product.ProductCategoryId == CommonConstants.ProductCategory.Paddy)
                .FirstOrDefaultAsync();

            if (chosen == null)
                throw new InvalidOperationException("Sản phẩm đã chọn không phải là biến thể lúa đang hoạt động. Vui lòng chọn một sản phẩm thuộc danh mục Lúa thô.");

            // Nếu phiếu có giống lúa, biến thể phải khớp giống lúa đó.
            if (receipt.RiceVarietyId.HasValue
                && chosen.RiceVarietyId.HasValue
                && chosen.RiceVarietyId.Value != receipt.RiceVarietyId.Value)
                throw new InvalidOperationException("Sản phẩm đã chọn không thuộc giống lúa của phiếu. Vui lòng chọn lại sản phẩm.");

            return chosen.Id;
        }

        // Dữ liệu cũ: phiếu chưa gắn biến thể → suy ra mặc định theo giống lúa.
        return await GetDefaultPaddyVariantIdAsync(receipt);
    }

    /// <summary>
    /// Tìm ProductVariantId đại diện cho lúa thô.
    /// </summary>
    private static string? ValidateBags(IReadOnlyCollection<DTOs.InboundOrders.CreateBagDto>? bags, decimal declaredWeight)
    {
        if (bags == null || bags.Count == 0) return null;
        if (bags.Any(x => x.BagNo <= 0 || x.WeightKg <= 0))
            return "Số thứ tự bao và khối lượng từng bao phải lớn hơn 0.";
        if (bags.Any(x => x.ScaleDeviceRef?.Trim().Length > 255))
            return "Mã thiết bị cân không được vượt quá 255 ký tự.";
        if (bags.Any(x => x.WeightCaptureMethod != null &&
            !string.Equals(x.WeightCaptureMethod.Trim(), "SCALE", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(x.WeightCaptureMethod.Trim(), "MANUAL", StringComparison.OrdinalIgnoreCase)))
            return "Phương thức ghi nhận cân chỉ được là SCALE hoặc MANUAL.";
        if (bags.Select(x => x.BagNo).Distinct().Count() != bags.Count)
            return "Số thứ tự bao không được trùng trong cùng phiếu.";
        var orderedNumbers = bags.Select(x => x.BagNo).OrderBy(x => x).ToArray();
        if (!orderedNumbers.SequenceEqual(Enumerable.Range(1, bags.Count)))
            return "Số thứ tự bao phải liên tục từ 1 đến số lượng bao.";
        var sum = bags.Sum(x => x.WeightKg);
        if (declaredWeight > 0 && Math.Abs(sum - declaredWeight) > 0.001m)
            return $"Tổng khối lượng các bao ({sum:0.###} kg) không khớp ActualWeightKg ({declaredWeight:0.###} kg).";
        return null;
    }

    private static void NormalizeBagTrace(IEnumerable<DTOs.InboundOrders.CreateBagDto>? bags, int currentUserId)
    {
        if (bags == null) return;
        foreach (var bag in bags)
        {
            bag.ScaleDeviceRef = string.IsNullOrWhiteSpace(bag.ScaleDeviceRef) ? null : bag.ScaleDeviceRef.Trim();
            bag.WeightCaptureMethod = string.IsNullOrWhiteSpace(bag.WeightCaptureMethod)
                ? "MANUAL" : bag.WeightCaptureMethod.Trim().ToUpperInvariant();
            bag.WeighedAt ??= DateTimeHelper.VietnamNow();
            // The actor is security-sensitive trace data; never trust a client-supplied user id.
            bag.WeighedBy = currentUserId;
        }
    }

    private static void NormalizeReceiptAmounts(CreatePaddyPurchaseReceiptDto dto)
    {
        if (dto.Bags?.Count > 0)
        {
            dto.ActualWeightKg = dto.Bags.Sum(x => x.WeightKg);
            dto.BagCount = dto.Bags.Count;
        }
        dto.TotalAmount = decimal.Round(dto.ActualWeightKg * dto.AgreedPrice, 2, MidpointRounding.AwayFromZero);
        dto.DebtAmount = Math.Max(0, dto.TotalAmount - dto.PaidAmount);
    }

    private static void NormalizeReceiptAmounts(UpdatePaddyPurchaseReceiptDto dto)
    {
        if (dto.Bags?.Count > 0)
        {
            dto.ActualWeightKg = dto.Bags.Sum(x => x.WeightKg);
            dto.BagCount = dto.Bags.Count;
        }
        dto.TotalAmount = decimal.Round(dto.ActualWeightKg * dto.AgreedPrice, 2, MidpointRounding.AwayFromZero);
        dto.DebtAmount = Math.Max(0, dto.TotalAmount - dto.PaidAmount);
    }

    private async Task<int> GetDefaultPaddyVariantIdAsync(PaddyPurchaseReceipt receipt)
    {
        if (receipt.RiceVarietyId.HasValue)
        {
            // Tìm variant lúa thô khớp chính xác giống lúa của phiếu.
            var matchedVariant = await _productVariantRepository
                .FindByCondition(x => x.RiceVarietyId == receipt.RiceVarietyId.Value
                    && !x.IsDeleted
                    && !x.IsByproduct
                    && x.IsActive
                    && x.Product.ProductCategoryId == CommonConstants.ProductCategory.Paddy)
                .FirstOrDefaultAsync();

            if (matchedVariant != null) return matchedVariant.Id;
        }

        // Dữ liệu cũ chưa gắn giống: chỉ fallback sang variant chung thuộc danh mục Lúa thô.
        var fallback = await _productVariantRepository
            .FindByCondition(x => !x.IsDeleted
                && !x.IsByproduct
                && x.IsActive
                && x.RiceVarietyId == null
                && x.Product.ProductCategoryId == CommonConstants.ProductCategory.Paddy)
            .FirstOrDefaultAsync();

        if (fallback != null) return fallback.Id;

        throw new InvalidOperationException("Không tìm thấy biến thể sản phẩm (ProductVariant) nào đại diện cho Lúa thô trong hệ thống để thực hiện chốt phiếu. Vui lòng cấu hình sản phẩm Lúa thô trước.");
    }


    private async Task RecordDebtAsync(PaddyPurchaseReceipt receipt, DateTime dueDate, int userId, DateTime now)
    {
        // Tìm hoặc tạo PartyDebt PAYABLE cho nông dân
        var partyDebt = await _partyDebtRepository.FirstOrDefaultAsync(x =>
            x.OrganizationId == receipt.OrganizationId &&
            x.PartyType == "FARMER" &&
            x.PartyId == receipt.FarmerId &&
            x.Direction == LookupCodes.DebtDirection.Payable &&
            !x.IsDeleted);

        if (partyDebt == null)
        {
            partyDebt = new PartyDebt
            {
                OrganizationId = receipt.OrganizationId,
                PartyType = "FARMER",
                PartyId = receipt.FarmerId,
                Direction = LookupCodes.DebtDirection.Payable,
                OpeningBalance = 0,
                CurrentBalance = 0,
                IsActive = true,
                CreatedBy = userId,
                CreatedDate = now
            };
            await _partyDebtRepository.CreateAsync(partyDebt);
            await _partyDebtRepository.SaveChangesAsync();
        }

        var balanceBefore = partyDebt.CurrentBalance;
        partyDebt.CurrentBalance += receipt.DebtAmount;
        partyDebt.LastModifiedDate = now;

        await _partyDebtRepository.UpdateAsync(partyDebt);

        var tx = new DebtTransaction
        {
            PartyDebtId = partyDebt.Id,
            TransactionType = LookupCodes.DebtTransactionType.Charge,
            Amount = receipt.DebtAmount,
            BalanceAfter = partyDebt.CurrentBalance,
            RefType = "PADDY_RECEIPT",
            RefId = receipt.Id,
            TransactionDate = receipt.ReceiptDate,
            DueDate = dueDate,
            Note = $"Phát sinh nợ từ phiếu mua lúa {receipt.ReceiptCode}",
            CreatedBy = userId,
            CreatedDate = now
        };

        await _debtTransactionRepository.CreateAsync(tx);
        await _debtTransactionRepository.SaveChangesAsync();

        if (_scheduledJobService != null)
        {
            _scheduledJobService.Enqueue<IDebtDueAndOverdueReminderService>(s => s.EvaluatePartyDebtAsync(partyDebt.Id, CancellationToken.None));
        }
    }

    public async Task UpdateScheduleStatusAsync(int scheduleId, int userId)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId);
        if (schedule == null || schedule.IsDeleted) return;

        // Quét tất cả các phiếu hoạt động thuộc lịch này
        var receipts = await _receiptRepository
            .FindByCondition(x => x.ScheduleId == scheduleId && !x.IsDeleted)
            .ToListAsync();

        if (receipts.Count == 0)
        {
            // Nếu không còn phiếu nào hoạt động dưới lịch, lùi lịch về trạng thái CONFIRMED (Đã xác nhận)
            var confirmedStatusId = _systemLookup.PaddyScheduleStatusId("CONFIRMED");
            var cancelledStatusId = _systemLookup.PaddyScheduleStatusId("CANCELLED");
            if (schedule.StatusId != cancelledStatusId && schedule.StatusId != confirmedStatusId)
            {
                schedule.StatusId = confirmedStatusId;
                schedule.UpdatedBy = userId;
                schedule.LastModifiedDate = DateTimeHelper.VietnamNow();
                await _scheduleRepository.UpdateAsync(schedule);
                await _scheduleRepository.SaveChangesAsync();
            }
            return;
        }

        var receiptWeights = new List<(decimal StoredWeightKg, decimal ActualWeightKg)>();
        foreach (var r in receipts)
        {
            // Put-away của lúa mua được thực hiện trong Inbound. Lượng đã nhập kho là
            // tổng QuantityReceived của các dòng thuộc phiếu Inbound liên kết receipt.
            var inboundOrder = await _inboundOrderRepository
                .FindByCondition(x =>
                    x.PaddyPurchaseReceiptId == r.Id &&
                    !x.IsDeleted,
                    false)
                .Include(x => x.InboundOrderItems)
                .FirstOrDefaultAsync();

            var totalStoredWeight = inboundOrder?.InboundOrderItems
                .Where(x => !x.IsDeleted)
                .Sum(x => x.QuantityReceived) ?? 0m;

            receiptWeights.Add((totalStoredWeight, r.ActualWeightKg));
        }

        var statusCode = PaddyScheduleStatusHelper.DetermineScheduleStatus(receiptWeights);
        int targetStatusId = _systemLookup.PaddyScheduleStatusId(statusCode);
        var scheduleCancelledStatusId = _systemLookup.PaddyScheduleStatusId("CANCELLED");

        // Chỉ cập nhật nếu không phải trạng thái CANCELLED và trạng thái mới khác trạng thái hiện tại
        if (schedule.StatusId != scheduleCancelledStatusId && schedule.StatusId != targetStatusId)
        {
            schedule.StatusId = targetStatusId;
            schedule.UpdatedBy = userId;
            schedule.LastModifiedDate = DateTimeHelper.VietnamNow();
            await _scheduleRepository.UpdateAsync(schedule);
            await _scheduleRepository.SaveChangesAsync();
        }
    }

    private static string? MapQualityStatus(string? qualityJson)
    {
        if (string.IsNullOrWhiteSpace(qualityJson)) return null;

        try
        {
            using var document = JsonDocument.Parse(qualityJson);
            if (!document.RootElement.TryGetProperty("grade", out var gradeElement)) return null;
            var grade = gradeElement.GetString()?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(grade)) return null;

            return grade.Contains("KHÔNG ĐẠT") ||
                   grade.Contains("KHONG DAT") ||
                   grade.Contains("CÁCH LY") ||
                   grade.Contains("CACH LY") ||
                   grade.Contains("CẦN XỬ LÝ") ||
                   grade.Contains("CAN XU LY")
                ? QualityStatusConstants.Failed
                : QualityStatusConstants.Passed;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
