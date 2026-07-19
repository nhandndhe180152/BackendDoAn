using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.PaddyPurchaseReceipts;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ phiếu mua lúa.
/// ConfirmReceiptAsync: chốt phiếu → sinh lô, tạo InboundOrder, cập nhật tồn, ghi công nợ.
/// </summary>
public class PaddyPurchaseReceiptService : IPaddyPurchaseReceiptService
{
    private readonly IPaddyPurchaseReceiptRepository _receiptRepository;
    private readonly IPaddyLotRepository _paddyLotRepository;
    private readonly IRepositoryBase<InboundOrder, int> _inboundOrderRepository;
    private readonly IRepositoryBase<InboundOrderItem, int> _inboundOrderItemRepository;
    private readonly IRepositoryBase<InboundOrderStatus, int> _inboundOrderStatusRepository;
    private readonly IRepositoryBase<LotStatus, int> _lotStatusRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryTransactionRepository _inventoryTransactionRepository;
    private readonly IRepositoryBase<PartyDebt, int> _partyDebtRepository;
    private readonly IRepositoryBase<DebtTransaction, int> _debtTransactionRepository;
    private readonly IProductVariantRepository _productVariantRepository;
    private readonly IPaddyPurchaseScheduleRepository _scheduleRepository;
    private readonly ISystemLookup _systemLookup;

    public PaddyPurchaseReceiptService(
        IPaddyPurchaseReceiptRepository receiptRepository,
        IPaddyLotRepository paddyLotRepository,
        IRepositoryBase<InboundOrder, int> inboundOrderRepository,
        IRepositoryBase<InboundOrderItem, int> inboundOrderItemRepository,
        IRepositoryBase<InboundOrderStatus, int> inboundOrderStatusRepository,
        IRepositoryBase<LotStatus, int> lotStatusRepository,
        IInventoryRepository inventoryRepository,
        IInventoryTransactionRepository inventoryTransactionRepository,
        IRepositoryBase<PartyDebt, int> partyDebtRepository,
        IRepositoryBase<DebtTransaction, int> debtTransactionRepository,
        IProductVariantRepository productVariantRepository,
        IPaddyPurchaseScheduleRepository scheduleRepository,
        ISystemLookup systemLookup)
    {
        _receiptRepository = receiptRepository;
        _paddyLotRepository = paddyLotRepository;
        _inboundOrderRepository = inboundOrderRepository;
        _inboundOrderItemRepository = inboundOrderItemRepository;
        _inboundOrderStatusRepository = inboundOrderStatusRepository;
        _lotStatusRepository = lotStatusRepository;
        _inventoryRepository = inventoryRepository;
        _inventoryTransactionRepository = inventoryTransactionRepository;
        _partyDebtRepository = partyDebtRepository;
        _debtTransactionRepository = debtTransactionRepository;
        _productVariantRepository = productVariantRepository;
        _scheduleRepository = scheduleRepository;
        _systemLookup = systemLookup;
    }

    public async Task<ApiResponse> CreateAsync(CreatePaddyPurchaseReceiptDto obj)
    {
        var datePart = DateTimeHelper.VietnamNow().ToString("yyyyMMdd");
        var baseCode = $"PPR-{datePart}";
        var count = await _receiptRepository.FindByCondition(x => x.ReceiptCode.StartsWith(baseCode)).CountAsync();
        var receiptCode = $"{baseCode}-{(count + 1):D4}";

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
            await UpdateScheduleStatusAsync(receipt.ScheduleId.Value, receipt.UpdatedBy ?? 1);
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
            await UpdateScheduleStatusAsync(scheduleId, 1);
        }

        return ApiResponse.Success(isDeleted);
    }

    /// <summary>
    /// Chốt phiếu: sinh PaddyLot, tạo InboundOrder + Item, cập nhật tồn, ghi công nợ.
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

        // 3. Lấy trạng thái lô mặc định của hệ thống
        var defaultLotStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => !x.IsDeleted) 
            ?? throw new InvalidOperationException("Không tìm thấy LotStatus nào trong hệ thống.");

        // Bọc toàn bộ trong DB Transaction đảm bảo tính nguyên tử (Atomic)
        await using var tx = await _receiptRepository.BeginTransactionAsync();
        try
        {
            // Xác định Id của Variant lúa mặc định
            var productVariantId = await GetDefaultPaddyVariantIdAsync(receipt);

            // 4. KHỞI TẠO LÔ HÀNG (PaddyLot):
            // - Đặt RemainingWeightKg (khối lượng còn lại) bằng ActualWeightKg của phiếu cân đầu vào.
            // - Đây là nguồn dữ liệu gốc của Lô hàng (Dashboard/Reports đọc trực tiếp từ đây).
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
                RemainingWeightKg = receipt.ActualWeightKg,
                CostPricePerKg = receipt.ActualWeightKg > 0 ? receipt.TotalAmount / receipt.ActualWeightKg : 0,
                QualityStatus = receipt.QualityJson,
                CreatedBy = confirmedById,
                CreatedDate = now
            };

            await _paddyLotRepository.CreateAsync(lot);
            await _paddyLotRepository.SaveChangesAsync();

            // 5. TẠO PHIẾU NHẬP KHO (InboundOrder) liên kết với receipt
            var inboundStatus = await _inboundOrderStatusRepository
                .FirstOrDefaultAsync(x => x.Name == InboundOrderStatusNames.Confirmed && !x.IsDeleted)
                ?? await _inboundOrderStatusRepository.FirstOrDefaultAsync(x => !x.IsDeleted)
                ?? throw new InvalidOperationException("Không tìm thấy InboundOrderStatus.");

            var inboundOrder = new InboundOrder
            {
                WarehouseId = receipt.WarehouseId,
                InboundOrderStatusId = inboundStatus.Id,
                PaddyPurchaseReceiptId = receiptId,
                OrganizationId = receipt.OrganizationId,
                SourceType = "RECEIPT",
                TotalAssetValue = receipt.TotalAmount,
                CompletedDate = receipt.ReceiptDate,
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
                QuantityReceived = receipt.ActualWeightKg,
                ActualWeightKg = receipt.ActualWeightKg,
                UnitCostPrice = lot.CostPricePerKg,
                CreatedBy = confirmedById,
                CreatedDate = now
            };

            await _inboundOrderItemRepository.CreateAsync(item);
            await _inboundOrderItemRepository.SaveChangesAsync();

            // 7. CẬP NHẬT TỒN KHO VẬT LÝ (Inventory):
            // - Hàm UpsertInventoryAsync sẽ chèn/cập nhật dòng tồn kho ở bảng Inventory có PaddyLotId tương ứng.
            // - Đồng thời ghi nhận giao dịch nhập kho ở bảng InventoryTransaction.
            await UpsertInventoryAsync(lot, receipt, inboundOrder.Id, item.Id, confirmedById, now);

            // 8. GHI NHẬN CÔNG NỢ (Record Debt) nếu số tiền nợ (DebtAmount) > 0
            if (receipt.DebtAmount > 0)
            {
                await RecordDebtAsync(receipt, confirmedById, now);
            }

            // 9. TỰ ĐỘNG CẬP NHẬT TRẠNG THÁI LỊCH HẸN THU MUA (UpdateScheduleStatusAsync):
            // - Dựa trên số lượng phiếu đã chốt và phiếu nháp thuộc lịch hẹn đó để tính toán trạng thái
            //   (WEIGHED -> PARTIALLY_STOCKED -> STOCKED).
            if (receipt.ScheduleId.HasValue)
            {
                await UpdateScheduleStatusAsync(receipt.ScheduleId.Value, confirmedById);
            }

            await _receiptRepository.EndTransactionAsync();

            return ApiResponse.Success(new { LotId = lot.Id, LotCode = lot.LotCode, InboundOrderId = inboundOrder.Id },
                "Chốt phiếu thành công. Đã sinh lô và phiếu nhập kho.");
        }
        catch (InvalidOperationException ex)
        {
            await _receiptRepository.RollbackTransactionAsync();
            return ApiResponse.UnprocessableEntity(ex.Message);
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

    private async Task UpsertInventoryAsync(PaddyLot lot, PaddyPurchaseReceipt receipt,
        int inboundOrderId, int inboundItemId, int userId, DateTime now)
    {
        var inventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
            lot.ProductVariantId, lot.WarehouseId, lot.LocationId, lot.Id);

        if (inventory == null)
        {
            inventory = new Inventory
            {
                WarehouseId = lot.WarehouseId,
                LocationId = lot.LocationId,
                ProductVariantId = lot.ProductVariantId,
                PaddyLotId = lot.Id,
                CostPrice = lot.CostPricePerKg,
                QuantityOnHand = 0,
                QuantityReserved = 0,
                CreatedDate = now
            };
            await _inventoryRepository.CreateAsync(inventory);
            await _inventoryRepository.SaveChangesAsync();
        }

        var before = inventory.QuantityOnHand;
        // Áp dụng bình quân gia quyền cho giá vốn khi nhập thêm hàng
        if (before > 0 && inventory.CostPrice > 0)
        {
            inventory.CostPrice = Math.Round(((before * inventory.CostPrice) + (receipt.ActualWeightKg * lot.CostPricePerKg)) / (before + receipt.ActualWeightKg), 2);
        }
        else
        {
            inventory.CostPrice = lot.CostPricePerKg;
        }

        inventory.QuantityOnHand += receipt.ActualWeightKg;
        inventory.LastModifiedDate = now;

        await _inventoryRepository.UpdateAsync(inventory);

        var tx = new InventoryTransaction
        {
            InventoryId = inventory.Id,
            WarehouseId = lot.WarehouseId,
            LocationId = lot.LocationId,
            ProductVariantId = lot.ProductVariantId,
            TransactionType = InventoryTransactionTypeConstants.Import,
            ReferenceType = InventoryReferenceTypeConstants.PaddyPurchase,
            ReferenceId = inboundOrderId,
            ReferenceItemId = inboundItemId,
            Quantity = receipt.ActualWeightKg,
            BeforeQuantity = before,
            AfterQuantity = inventory.QuantityOnHand,
            WeightKg = receipt.ActualWeightKg,
            Note = $"Nhập lúa lô {lot.LotCode}",
            CreatedDate = now,
            CreatedBy = userId
        };

        await _inventoryTransactionRepository.CreateAsync(tx);
        await _inventoryTransactionRepository.SaveChangesAsync();
    }

    private async Task RecordDebtAsync(PaddyPurchaseReceipt receipt, int userId, DateTime now)
    {
        // Tìm hoặc tạo PartyDebt PAYABLE cho nông dân
        var partyDebt = await _partyDebtRepository.FirstOrDefaultAsync(x =>
            x.OrganizationId == receipt.OrganizationId &&
            x.PartyType == "FARMER" &&
            x.PartyId == receipt.FarmerId &&
            x.Direction == "PAYABLE" &&
            !x.IsDeleted);

        if (partyDebt == null)
        {
            partyDebt = new PartyDebt
            {
                OrganizationId = receipt.OrganizationId,
                PartyType = "FARMER",
                PartyId = receipt.FarmerId,
                Direction = "PAYABLE",
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
            TransactionType = "CHARGE",
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
    }

    private async Task UpdateScheduleStatusAsync(int scheduleId, int userId)
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

        // Lấy danh sách ID của các phiếu đã chốt (đã sinh lô lúa PaddyLot)
        var confirmedReceiptIds = await _paddyLotRepository
            .FindByCondition(x => !x.IsDeleted && x.SourceReceiptId != null)
            .Select(x => x.SourceReceiptId!.Value)
            .ToListAsync();

        var confirmedCount = receipts.Count(r => confirmedReceiptIds.Contains(r.Id));
        var pendingCount = receipts.Count - confirmedCount;

        int targetStatusId;

        if (confirmedCount > 0 && pendingCount > 0)
        {
            // Kịch bản 1: Có cả phiếu đã chốt và phiếu nháp -> Nhập kho một phần (PARTIALLY_STOCKED)
            targetStatusId = _systemLookup.PaddyScheduleStatusId("PARTIALLY_STOCKED");
        }
        else if (confirmedCount > 0 && pendingCount == 0)
        {
            // Kịch bản 2: Toàn bộ phiếu hoạt động đều đã chốt -> Đã nhập kho (STOCKED)
            targetStatusId = _systemLookup.PaddyScheduleStatusId("STOCKED");
        }
        else
        {
            // Kịch bản 3: Chưa chốt phiếu nào -> Đã cân hàng (WEIGHED)
            targetStatusId = _systemLookup.PaddyScheduleStatusId("WEIGHED");
        }

        var scheduleCancelledStatusId = _systemLookup.PaddyScheduleStatusId("CANCELLED");
        // Giữ nguyên trạng thái nếu lịch đã bị hủy, chỉ đổi nếu trạng thái thực tế tính toán khác trạng thái lịch hiện tại
        if (schedule.StatusId != scheduleCancelledStatusId && schedule.StatusId != targetStatusId)
        {
            schedule.StatusId = targetStatusId;
            schedule.UpdatedBy = userId;
            schedule.LastModifiedDate = DateTimeHelper.VietnamNow();
            await _scheduleRepository.UpdateAsync(schedule);
            await _scheduleRepository.SaveChangesAsync();
        }
    }
}
