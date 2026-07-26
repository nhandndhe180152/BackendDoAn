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

    public PaddyPurchaseReceiptService(
        IPaddyPurchaseReceiptRepository receiptRepository,
        IPaddyLotRepository paddyLotRepository,
        IRepositoryBase<InboundOrder, int> inboundOrderRepository,
        IRepositoryBase<InboundOrderItem, int> inboundOrderItemRepository,
        IRepositoryBase<InboundOrderStatus, int> inboundOrderStatusRepository,
        IRepositoryBase<LotStatus, int> lotStatusRepository,
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
        IScheduledJobService? scheduledJobService = null)
    {
        _receiptRepository = receiptRepository;
        _paddyLotRepository = paddyLotRepository;
        _inboundOrderRepository = inboundOrderRepository;
        _inboundOrderItemRepository = inboundOrderItemRepository;
        _inboundOrderStatusRepository = inboundOrderStatusRepository;
        _lotStatusRepository = lotStatusRepository;
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
    }

    private int GetCurrentUserId()
        => _httpContextAccessor.HttpContext?.GetCurrentUserId() ?? 1;

    public async Task<ApiResponse> CreateAsync(CreatePaddyPurchaseReceiptDto obj)
    {
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
        var entities = await _receiptRepository
            .FindByCondition(x => !x.IsDeleted, false, x => x.Farmer, x => x.Warehouse)
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
        var existData = await _receiptRepository.GetByIdAsync(obj.Id);
        if (existData == null || existData.IsDeleted) return ApiResponse.NotFound();

        // Không cho sửa phiếu đã chốt
        var isConfirmed = await _paddyLotRepository.AnyAsync(x => x.SourceReceiptId == obj.Id && !x.IsDeleted);
        if (isConfirmed)
            return ApiResponse.UnprocessableEntity("Phiếu đã được chốt, không thể chỉnh sửa.");

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
    public async Task<ApiResponse> ConfirmReceiptAsync(int receiptId, int confirmedById)
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

        // 3. Lấy trạng thái lô mặc định của hệ thống
        var defaultLotStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => !x.IsDeleted) 
            ?? throw new InvalidOperationException("Không tìm thấy LotStatus nào trong hệ thống.");

        // Bọc toàn bộ trong DB Transaction đảm bảo tính nguyên tử (Atomic)
        await using var tx = await _receiptRepository.BeginTransactionAsync();
        try
        {
            // Xác định Id của Variant lúa mặc định
            var productVariantId = await GetDefaultPaddyVariantIdAsync(receipt);

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

            // 5. TẠO PHIẾU NHẬP KHO (InboundOrder) liên kết với receipt
            var inboundStatus = await _inboundOrderStatusRepository
                .FirstOrDefaultAsync(x => x.Name == InboundOrderStatusNames.Draft && !x.IsDeleted)
                ?? await _inboundOrderStatusRepository.FirstOrDefaultAsync(x => !x.IsDeleted)
                ?? throw new InvalidOperationException("Không tìm thấy InboundOrderStatus.");

            var inboundBaseCode = $"INB-{datePart}";
            var cntToday = await _inboundOrderRepository
                .FindByCondition(x => x.POCode != null && x.POCode.StartsWith(inboundBaseCode))
                .CountAsync();
            var inbCode = $"{inboundBaseCode}-{(cntToday + 1):D4}";
            int attemptsInb = 0;
            while (await _inboundOrderRepository.AnyAsync(x => x.POCode == inbCode && x.OrganizationId == receipt.OrganizationId) && attemptsInb < 10)
            {
                attemptsInb++;
                inbCode = $"{inboundBaseCode}-{(cntToday + 1 + attemptsInb):D4}";
            }

            var inboundOrder = new InboundOrder
            {
                WarehouseId = receipt.WarehouseId,
                InboundOrderStatusId = inboundStatus.Id,
                POCode = inbCode,
                PaddyPurchaseReceiptId = receiptId,
                OrganizationId = receipt.OrganizationId,
                SourceType = "RECEIPT",
                TotalAssetValue = receipt.TotalAmount,
                ExpectedDate = receipt.ReceiptDate,
                CompletedDate = null,
                Note = $"Nhập lúa từ phiếu mua {receipt.ReceiptCode}",
                CreatedBy = confirmedById,
                CreatedDate = now
            };

            await _inboundOrderRepository.CreateAsync(inboundOrder);
            await _inboundOrderRepository.SaveChangesAsync();

            // 6. TẠO CHI TIẾT PHIẾU NHẬP KHO (InboundOrderItem) liên kết chặt chẽ với Lô hàng vừa sinh
            var item = new InboundOrderItem
            {
                InboundOrderId = inboundOrder.Id,
                ProductVariantId = lot.ProductVariantId,
                PaddyLotId = lot.Id,
                QuantityOrdered = receipt.ActualWeightKg,
                QuantityReceived = 0m,
                ExpectedWeightKg = receipt.ActualWeightKg,
                ActualWeightKg = receipt.ActualWeightKg,
                UnitCostPrice = lot.CostPricePerKg,
                CreatedBy = confirmedById,
                CreatedDate = now
            };

            await _inboundOrderItemRepository.CreateAsync(item);
            await _inboundOrderItemRepository.SaveChangesAsync();

            // Không tạo tồn khu đệm và không tạo InventoryTransaction tại bước chốt.

            // 7. GHI NHẬN CÔNG NỢ (Record Debt) nếu số tiền nợ (DebtAmount) > 0
            if (receipt.DebtAmount > 0)
            {
                await RecordDebtAsync(receipt, confirmedById, now);
            }

            // 8. Lịch chỉ chuyển sang WEIGHED; STOCKED chỉ sau khi Inbound xác nhận đủ.
            if (receipt.ScheduleId.HasValue)
            {
                await UpdateScheduleStatusAsync(receipt.ScheduleId.Value, confirmedById);
            }

            await _receiptRepository.EndTransactionAsync();

            // Thông báo cho Chủ kho và Nhân viên kho: phiếu mua lúa đã chốt, đã sinh lô + phiếu nhập.
            await _notificationDispatcher.DispatchAsync(
                NotificationConstants.Code.PurchaseReceiptConfirmed,
                new NotificationTarget { RoleIds = new List<int> { CommonConstants.Role.OWNER, CommonConstants.Role.WAREHOUSE } },
                new object[] { receipt.ReceiptCode },
                "/admin/rice-purchase",
                confirmedById);

            return ApiResponse.Success(new { LotId = lot.Id, LotCode = lot.LotCode, InboundOrderId = inboundOrder.Id },
                "Chốt phiếu thành công. Đã sinh lô và phiếu nhập kho.");
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

    // ── Private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Tìm ProductVariantId đại diện cho lúa thô. 
    /// </summary>
    private async Task<int> GetDefaultPaddyVariantIdAsync(PaddyPurchaseReceipt receipt)
    {
        if (receipt.RiceVarietyId.HasValue)
        {
            // Tìm variant khớp RiceVarietyId, không phải byproduct, và tên sản phẩm/category chứa từ "Lúa"
            var matchedVariant = await _productVariantRepository
                .FindByCondition(x => x.RiceVarietyId == receipt.RiceVarietyId.Value 
                    && !x.IsByproduct 
                    && x.IsActive 
                    && (x.Product.ProductCategory.Name.Contains("Lúa") || x.Name.Contains("Lúa") || x.Product.Name.Contains("Lúa")))
                .FirstOrDefaultAsync();

            if (matchedVariant != null) return matchedVariant.Id;

            // Fallback 1: bỏ qua điều kiện tên chứa "Lúa"
            matchedVariant = await _productVariantRepository
                .FindByCondition(x => x.RiceVarietyId == receipt.RiceVarietyId.Value && !x.IsByproduct && x.IsActive)
                .FirstOrDefaultAsync();

            if (matchedVariant != null) return matchedVariant.Id;
        }

        // Fallback 2: Tìm variant đầu tiên không phải byproduct và thuộc category "Lúa"
        var fallback = await _productVariantRepository
            .FindByCondition(x => !x.IsByproduct && x.IsActive 
                && (x.Product.ProductCategory.Name.Contains("Lúa") || x.Name.Contains("Lúa") || x.Product.Name.Contains("Lúa")))
            .FirstOrDefaultAsync();

        if (fallback != null) return fallback.Id;

        // Fallback 3: Tìm variant active không phải byproduct bất kỳ
        fallback = await _productVariantRepository
            .FindByCondition(x => !x.IsByproduct && x.IsActive)
            .FirstOrDefaultAsync();

        if (fallback != null) return fallback.Id;

        throw new InvalidOperationException("Không tìm thấy biến thể sản phẩm (ProductVariant) nào đại diện cho Lúa thô trong hệ thống để thực hiện chốt phiếu. Vui lòng cấu hình sản phẩm Lúa thô trước.");
    }


    private async Task RecordDebtAsync(PaddyPurchaseReceipt receipt, int userId, DateTime now)
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
