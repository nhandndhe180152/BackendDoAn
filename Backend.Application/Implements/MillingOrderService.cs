using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.MillingOrders;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ lệnh xay xát (MillingOrder).
/// CompleteMillingOrderAsync: trừ lúa đầu vào, sinh lô gạo/phụ phẩm, nhập kho đầu ra.
/// </summary>
public class MillingOrderService : IMillingOrderService
{
    private readonly IMillingOrderRepository _millingOrderRepository;
    private readonly IPaddyLotRepository _paddyLotRepository;
    private readonly IRepositoryBase<MillingOrderInput, int> _inputRepository;
    private readonly IRepositoryBase<MillingOrderOutput, int> _outputRepository;
    private readonly IRepositoryBase<MillingOrderStatus, int> _statusRepository;
    private readonly IRepositoryBase<LotStatus, int> _lotStatusRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryTransactionRepository _inventoryTransactionRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly INotificationDispatcher _notificationDispatcher;

    public MillingOrderService(
        IMillingOrderRepository millingOrderRepository,
        IPaddyLotRepository paddyLotRepository,
        IRepositoryBase<MillingOrderInput, int> inputRepository,
        IRepositoryBase<MillingOrderOutput, int> outputRepository,
        IRepositoryBase<MillingOrderStatus, int> statusRepository,
        IRepositoryBase<LotStatus, int> lotStatusRepository,
        IInventoryRepository inventoryRepository,
        IInventoryTransactionRepository inventoryTransactionRepository,
        ILocationRepository locationRepository,
        INotificationDispatcher notificationDispatcher)
    {
        _millingOrderRepository = millingOrderRepository;
        _paddyLotRepository = paddyLotRepository;
        _inputRepository = inputRepository;
        _outputRepository = outputRepository;
        _statusRepository = statusRepository;
        _lotStatusRepository = lotStatusRepository;
        _inventoryRepository = inventoryRepository;
        _inventoryTransactionRepository = inventoryTransactionRepository;
        _locationRepository = locationRepository;
        _notificationDispatcher = notificationDispatcher;
    }

    /// <summary>
    /// Kiểm tra lô có đang bị CÁCH LY hay không (#2/#11).
    /// Chỉ chặn đúng trạng thái QUARANTINE — KHÔNG dùng IsSellable, vì lúa nguyên liệu
    /// (PADDY) mặc định ở trạng thái PENDING_INBOUND có IsSellable=false nhưng vẫn hợp lệ để đưa vào xay.
    /// </summary>
    private async Task<bool> IsLotBlockedForMillingAsync(int statusId)
    {
        var status = await _lotStatusRepository.GetByIdAsync(statusId);
        return status != null && status.Code == LotStatusCodeConstants.Quarantine;
    }

    public async Task<ApiResponse> CreateAsync(CreateMillingOrderDto obj)
    {
        // #17: Validate tỷ lệ thu hồi (yield) và khối lượng gạo mục tiêu
        if (obj.ExpectedYield <= 0 || obj.ExpectedYield > 1)
            return ApiResponse.UnprocessableEntity("Tỷ lệ thu hồi (ExpectedYield) phải nằm trong khoảng (0, 1]. Ví dụ 0.65 = 65%.");
        if (obj.TargetRiceKg <= 0)
            return ApiResponse.UnprocessableEntity("Khối lượng gạo mục tiêu (TargetRiceKg) phải lớn hơn 0.");

        var now = DateTimeHelper.VietnamNow();
        var datePart = now.ToString("yyyyMMdd");
        var baseCode = $"MO-{datePart}";
        var count = await _millingOrderRepository
            .FindByCondition(x => x.MillingCode.StartsWith(baseCode))
            .CountAsync();
        var millingCode = $"{baseCode}-{(count + 1):D4}";

        // #5: Retry chống trùng mã khi nhiều request chạy đồng thời
        int codeAttempts = 0;
        while (await _millingOrderRepository.FindByCondition(x => x.MillingCode == millingCode).AnyAsync() && codeAttempts < 10)
        {
            codeAttempts++;
            millingCode = $"{baseCode}-{(count + 1 + codeAttempts):D4}";
        }

        var draftStatus = await _statusRepository.FirstOrDefaultAsync(x => x.Code == LookupCodes.MillingOrderStatus.Draft && !x.IsDeleted)
            ?? throw new InvalidOperationException("Không tìm thấy MillingOrderStatus DRAFT.");

        var computedPaddyKg = obj.ExpectedYield > 0 ? obj.TargetRiceKg / obj.ExpectedYield : 0;

        var order = new MillingOrder
        {
            OrganizationId = obj.OrganizationId,
            MillingCode = millingCode,
            StatusId = draftStatus.Id,
            WarehouseId = obj.WarehouseId,
            Reason = obj.Reason?.Trim(),
            SalesOrderId = obj.SalesOrderId,
            YieldRateUsed = obj.ExpectedYield,
            TotalRiceOutputKg = obj.TargetRiceKg,
            ComputedPaddyKg = computedPaddyKg,
            CreatedBy = obj.CreatedBy,
            CreatedDate = now
        };

        await _millingOrderRepository.CreateAsync(order);
        await _millingOrderRepository.SaveChangesAsync();

        // Thông báo cho Nhân viên xay xát và Nhân viên kho: có lệnh xay mới.
        await _notificationDispatcher.DispatchAsync(
            NotificationConstants.Code.MillingOrderCreated,
            new NotificationTarget { RoleIds = new List<int> { CommonConstants.Role.MILLING, CommonConstants.Role.WAREHOUSE } },
            new object[] { millingCode },
            "/admin/milling-orders",
            obj.CreatedBy);

        return ApiResponse.Created(order.Id, "Tạo lệnh xay thành công.");
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateMillingOrderDto> objs)
        => Task.FromResult(ApiResponse.Error(message: "CreateList chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> GetAllAsync()
    {
        var entities = await _millingOrderRepository
            .FindByCondition(x => !x.IsDeleted, false, x => x.Status, x => x.Warehouse)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        return ApiResponse.Success(entities.Select(x => ToDetailDto(x)).ToList());
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var entity = await _millingOrderRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted,
                false,
                x => x.Status,
                x => x.Warehouse,
                x => x.MillingOrderInputs,
                x => x.MillingOrderOutputs)
            .FirstOrDefaultAsync();

        if (entity == null) return ApiResponse.NotFound();
        return ApiResponse.Success(ToDetailDto(entity));
    }

    /// <summary>Gap 2: Lấy các lệnh xay gắn với một đơn bán (điều phối xay-theo-đơn).</summary>
    public async Task<ApiResponse> GetBySalesOrderAsync(int salesOrderId)
    {
        var entities = await _millingOrderRepository
            .FindByCondition(x => x.SalesOrderId == salesOrderId && !x.IsDeleted, false, x => x.Status, x => x.Warehouse)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        return ApiResponse.Success(entities.Select(x => ToDetailDto(x)).ToList());
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _millingOrderRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query)
        => Task.FromResult(ApiResponse.Error(message: "GetPaged (SearchQuery) chưa được hỗ trợ.", status: 501));
    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
        => Task.FromResult(ApiResponse.Error(message: "GetPaged (AdvancedSearchQuery) chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> UpdateAsync(UpdateMillingOrderDto obj)
    {
        var existData = await _millingOrderRepository
            .FindByCondition(x => x.Id == obj.Id && !x.IsDeleted, false, x => x.Status)
            .FirstOrDefaultAsync();
        if (existData == null) return ApiResponse.NotFound();

        if (existData.Status?.Code != LookupCodes.MillingOrderStatus.Draft)
            return ApiResponse.UnprocessableEntity("Chỉ có thể chỉnh sửa lệnh xay ở trạng thái Nháp.");

        // #17: Validate tỷ lệ thu hồi (yield) và khối lượng gạo mục tiêu
        if (obj.ExpectedYield <= 0 || obj.ExpectedYield > 1)
            return ApiResponse.UnprocessableEntity("Tỷ lệ thu hồi (ExpectedYield) phải nằm trong khoảng (0, 1]. Ví dụ 0.65 = 65%.");
        if (obj.TargetRiceKg <= 0)
            return ApiResponse.UnprocessableEntity("Khối lượng gạo mục tiêu (TargetRiceKg) phải lớn hơn 0.");

        existData.OrganizationId = obj.OrganizationId;
        existData.WarehouseId = obj.WarehouseId;
        existData.Reason = obj.Reason?.Trim();
        existData.SalesOrderId = obj.SalesOrderId;
        existData.YieldRateUsed = obj.ExpectedYield;
        existData.TotalRiceOutputKg = obj.TargetRiceKg;
        existData.ComputedPaddyKg = obj.ExpectedYield > 0 ? obj.TargetRiceKg / obj.ExpectedYield : 0;
        existData.UpdatedBy = obj.UpdatedBy;
        existData.LastModifiedDate = DateTimeHelper.VietnamNow();

        await _millingOrderRepository.UpdateAsync(existData);
        await _millingOrderRepository.SaveChangesAsync();

        return ApiResponse.Success(existData.Id, "Cập nhật lệnh xay thành công.");
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateMillingOrderDto> objs)
        => Task.FromResult(ApiResponse.Error(message: "UpdateList chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _millingOrderRepository.SoftDeleteAsync(id);
        if (!isDeleted) return ApiResponse.BadRequest();
        await _millingOrderRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _millingOrderRepository.SoftDeleteListAsync(objs);
        if (!isDeleted) return ApiResponse.BadRequest();
        await _millingOrderRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> ReserveAsync(int id, ReserveMillingOrderDto dto, int userId)
    {
        var order = await _millingOrderRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted, false, x => x.Status)
            .FirstOrDefaultAsync();

        if (order == null) return ApiResponse.Error("Không tìm thấy lệnh xay.", 404);
        if (order.Status?.Code != LookupCodes.MillingOrderStatus.Draft)
            return ApiResponse.UnprocessableEntity("Chỉ có thể giữ lúa cho lệnh xay ở trạng thái Nháp.");

        var reservedStatus = await _statusRepository.FirstOrDefaultAsync(x => x.Code == LookupCodes.MillingOrderStatus.Reserved && !x.IsDeleted)
            ?? throw new InvalidOperationException("Không tìm thấy trạng thái RESERVED.");

        var now = DateTimeHelper.VietnamNow();

        await using var tx = await _millingOrderRepository.BeginTransactionAsync();
        try
        {
            foreach (var inputDto in dto.Inputs)
            {
                var lot = await _paddyLotRepository.GetByIdAsync(inputDto.PaddyLotId);
                if (lot == null) return ApiResponse.Error($"Không tìm thấy lô lúa {inputDto.PaddyLotId}", 404);

                // #2/#11: Chặn đưa lô đang CÁCH LY / không đủ điều kiện vào xay
                if (await IsLotBlockedForMillingAsync(lot.StatusId))
                    return ApiResponse.UnprocessableEntity($"Lô {lot.LotCode} đang bị cách ly hoặc không đủ điều kiện để đưa vào xay xát.");

                if (lot.RemainingWeightKg < inputDto.ConsumedWeightKg)
                    return ApiResponse.UnprocessableEntity($"Lô {lot.LotCode} không đủ lúa (còn {lot.RemainingWeightKg} kg, yêu cầu {inputDto.ConsumedWeightKg} kg).");

                var targetLocationId = inputDto.LocationId ?? lot.LocationId;
                var inventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                    lot.ProductVariantId, lot.WarehouseId, targetLocationId, lot.Id);

                if (inventory == null || inventory.QuantityOnHand - inventory.QuantityReserved < inputDto.ConsumedWeightKg)
                    return ApiResponse.UnprocessableEntity($"Lô {lot.LotCode} không đủ tồn kho khả dụng để giữ.");

                // Hold inventory
                inventory.QuantityReserved += inputDto.ConsumedWeightKg;
                inventory.LastModifiedDate = now;
                await _inventoryRepository.UpdateAsync(inventory);

                // Create MillingOrderInput
                var input = new MillingOrderInput
                {
                    MillingOrderId = order.Id,
                    PaddyLotId = inputDto.PaddyLotId,
                    LocationId = inputDto.LocationId,
                    ConsumedWeightKg = inputDto.ConsumedWeightKg,
                    ReservedWeightKg = inputDto.ConsumedWeightKg, // Same for reserve
                    Note = inputDto.Note?.Trim(),
                    CreatedBy = userId,
                    CreatedDate = now
                };
                await _inputRepository.CreateAsync(input);
            }

            order.StatusId = reservedStatus.Id;
            order.UpdatedBy = userId;
            order.LastModifiedDate = now;
            await _millingOrderRepository.UpdateAsync(order);

            await _millingOrderRepository.SaveChangesAsync();
            await tx.CommitAsync();

            return ApiResponse.Success(order.Id, "Giữ lúa thành công.");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<ApiResponse> StartAsync(int id, int userId)
    {
        var order = await _millingOrderRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted, false, x => x.Status)
            .FirstOrDefaultAsync();

        if (order == null) return ApiResponse.Error("Không tìm thấy lệnh xay.", 404);
        if (order.Status?.Code != LookupCodes.MillingOrderStatus.Reserved)
            return ApiResponse.UnprocessableEntity("Chỉ có thể bắt đầu lệnh xay đã được Giữ lúa.");

        var inProgressStatus = await _statusRepository.FirstOrDefaultAsync(x => x.Code == LookupCodes.MillingOrderStatus.InProgress && !x.IsDeleted)
            ?? throw new InvalidOperationException("Không tìm thấy trạng thái IN_PROGRESS.");

        order.StatusId = inProgressStatus.Id;
        order.StartedAt = DateTimeHelper.VietnamNow();
        order.UpdatedBy = userId;
        order.LastModifiedDate = DateTimeHelper.VietnamNow();

        await _millingOrderRepository.UpdateAsync(order);
        await _millingOrderRepository.SaveChangesAsync();

        return ApiResponse.Success(order.Id, "Đã bắt đầu lệnh xay.");
    }

    public async Task<ApiResponse> CancelAsync(int id, int userId)
    {
        var order = await _millingOrderRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted, false, x => x.Status, x => x.MillingOrderInputs)
            .FirstOrDefaultAsync();

        if (order == null) return ApiResponse.Error("Không tìm thấy lệnh xay.", 404);
        
        var currentCode = order.Status?.Code;
        if (currentCode == LookupCodes.MillingOrderStatus.Completed || currentCode == LookupCodes.MillingOrderStatus.Cancelled)
            return ApiResponse.UnprocessableEntity($"Không thể hủy lệnh xay ở trạng thái {currentCode}.");

        var cancelStatus = await _statusRepository.FirstOrDefaultAsync(x => x.Code == LookupCodes.MillingOrderStatus.Cancelled && !x.IsDeleted)
            ?? throw new InvalidOperationException("Không tìm thấy trạng thái CANCELLED.");

        var now = DateTimeHelper.VietnamNow();

        await using var tx = await _millingOrderRepository.BeginTransactionAsync();
        try
        {
            // If Reserved or InProgress, we need to release QuantityReserved
            if (currentCode == LookupCodes.MillingOrderStatus.Reserved || currentCode == LookupCodes.MillingOrderStatus.InProgress)
            {
                foreach (var input in order.MillingOrderInputs)
                {
                    var lot = await _paddyLotRepository.GetByIdAsync(input.PaddyLotId);
                    if (lot != null)
                    {
                        var targetLocationId = input.LocationId ?? lot.LocationId;
                        var inventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                            lot.ProductVariantId, lot.WarehouseId, targetLocationId, lot.Id);

                        if (inventory != null)
                        {
                            inventory.QuantityReserved = Math.Max(0, inventory.QuantityReserved - input.ReservedWeightKg.GetValueOrDefault());
                            inventory.LastModifiedDate = now;
                            await _inventoryRepository.UpdateAsync(inventory);
                        }
                    }
                }
            }

            order.StatusId = cancelStatus.Id;
            order.UpdatedBy = userId;
            order.LastModifiedDate = now;
            await _millingOrderRepository.UpdateAsync(order);

            await _millingOrderRepository.SaveChangesAsync();
            await tx.CommitAsync();

            return ApiResponse.Success(order.Id, "Hủy lệnh xay thành công.");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Hoàn thành lệnh xay:
    /// </summary>
    public async Task<ApiResponse> CompleteMillingOrderAsync(int orderId, CompleteMillingOrderDto dto, int completedById)
    {
        var order = await _millingOrderRepository
            .FindByCondition(x => x.Id == orderId && !x.IsDeleted,
                false,
                x => x.Status,
                x => x.MillingOrderInputs,
                x => x.MillingOrderOutputs)
            .FirstOrDefaultAsync();

        if (order == null) return ApiResponse.NotFound();

        if (order.Status?.Code != LookupCodes.MillingOrderStatus.InProgress && order.Status?.Code != LookupCodes.MillingOrderStatus.Reserved)
            return ApiResponse.UnprocessableEntity("Chỉ hoàn thành lệnh xay khi đang Xay hoặc Đã giữ lúa.");

        var completedStatus = await _statusRepository.FirstOrDefaultAsync(x => x.Code == LookupCodes.MillingOrderStatus.Completed && !x.IsDeleted);

        var now = DateTimeHelper.VietnamNow();

        var totalConsumedKg = order.MillingOrderInputs.Sum(i => i.ConsumedWeightKg);
        var totalProducedKg = dto.Outputs.Sum(o => o.OutputWeightKg);
        var lossKg = dto.LossKg ?? 0;
        var massBalanceTolerance = totalConsumedKg * 0.02m;
        
        // #17: Đầu ra phải > 0
        if (totalProducedKg <= 0)
            return ApiResponse.UnprocessableEntity("Tổng khối lượng đầu ra phải lớn hơn 0.");

        if (totalConsumedKg > 0 && (totalProducedKg + lossKg) > (totalConsumedKg + massBalanceTolerance))
        {
            return ApiResponse.UnprocessableEntity(
                $"Mass balance không hợp lệ: Tổng đầu ra ({totalProducedKg} kg) + Hao hụt ({lossKg} kg) " +
                $"vượt quá tổng lúa đầu vào ({totalConsumedKg} kg) ± 2% dung sai.");
        }

        // #17: Chặn cận dưới — tổng đầu ra + hao hụt không được thấp hơn đầu vào quá dung sai
        // (tránh khai báo output/loss quá thấp làm "bốc hơi" lúa không rõ nguyên nhân).
        if (totalConsumedKg > 0 && (totalProducedKg + lossKg) < (totalConsumedKg - massBalanceTolerance))
        {
            return ApiResponse.UnprocessableEntity(
                $"Mass balance không hợp lệ: Tổng đầu ra ({totalProducedKg} kg) + Hao hụt ({lossKg} kg) " +
                $"thấp hơn tổng lúa đầu vào ({totalConsumedKg} kg) quá 2% dung sai. Vui lòng khai báo đủ hao hụt/phụ phẩm.");
        }

        await using var tx = await _millingOrderRepository.BeginTransactionAsync();
        try
        {
            decimal totalMaterialCost = 0;
            foreach (var input in order.MillingOrderInputs)
            {
                var lot = await _paddyLotRepository.GetByIdAsync(input.PaddyLotId);
                if (lot == null) return ApiResponse.Error($"Không tìm thấy lô lúa Id={input.PaddyLotId}.", 404);

                // #2/#11: Chặn hoàn thành nếu lô đã bị chuyển sang CÁCH LY sau khi giữ lúa
                if (await IsLotBlockedForMillingAsync(lot.StatusId))
                    return ApiResponse.UnprocessableEntity($"Lô {lot.LotCode} đang bị cách ly hoặc không đủ điều kiện để xay xát.");

                if (lot.RemainingWeightKg < input.ConsumedWeightKg)
                    return ApiResponse.UnprocessableEntity(
                        $"Lô {lot.LotCode}: tồn kho lúa ({lot.RemainingWeightKg} kg) không đủ để xay ({input.ConsumedWeightKg} kg).");

                totalMaterialCost += input.ConsumedWeightKg * lot.CostPricePerKg;

                lot.RemainingWeightKg -= input.ConsumedWeightKg;
                lot.LastModifiedDate = now;
                await _paddyLotRepository.UpdateAsync(lot);

                // Export physical inventory AND release reservation
                var targetLocationId = input.LocationId ?? lot.LocationId;
                var inventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                    lot.ProductVariantId, lot.WarehouseId, targetLocationId, lot.Id);

                if (inventory == null || inventory.QuantityOnHand < input.ConsumedWeightKg)
                    throw new InvalidOperationException($"Không đủ tồn kho vật lý lô {lot.LotCode}");

                var before = inventory.QuantityOnHand;
                inventory.QuantityOnHand -= input.ConsumedWeightKg;
                inventory.QuantityReserved = Math.Max(0, inventory.QuantityReserved - input.ReservedWeightKg.GetValueOrDefault());
                inventory.LastModifiedDate = now;
                await _inventoryRepository.UpdateAsync(inventory);

                var invTx = new InventoryTransaction
                {
                    InventoryId = inventory.Id,
                    WarehouseId = lot.WarehouseId,
                    LocationId = targetLocationId,
                    ProductVariantId = lot.ProductVariantId,
                    TransactionType = InventoryTransactionTypeConstants.Export,
                    ReferenceType = InventoryReferenceTypeConstants.MillingOrder,
                    ReferenceId = orderId,
                    ReferenceItemId = input.Id,
                    Quantity = -input.ConsumedWeightKg,
                    BeforeQuantity = before,
                    AfterQuantity = inventory.QuantityOnHand,
                    WeightKg = input.ConsumedWeightKg,
                    Note = $"Xay lúa lô {lot.LotCode}",
                    CreatedDate = now,
                    CreatedBy = completedById
                };
                await _inventoryTransactionRepository.CreateAsync(invTx);
            }

            await _paddyLotRepository.SaveChangesAsync();

            decimal totalCostToAllocate = totalMaterialCost + (order.TotalCost ?? 0);
            decimal explicitlyAssignedCost = dto.Outputs.Where(x => x.UnitCost.HasValue).Sum(x => x.OutputWeightKg * x.UnitCost.Value);
            decimal remainingWeightToAllocate = dto.Outputs.Where(x => !x.UnitCost.HasValue).Sum(x => x.OutputWeightKg);
            decimal remainingCost = Math.Max(0, totalCostToAllocate - explicitlyAssignedCost);
            decimal defaultUnitCost = remainingWeightToAllocate > 0 ? remainingCost / remainingWeightToAllocate : 0;

            var defaultLotStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == LotStatusCodeConstants.InStock && !x.IsDeleted)
                ?? throw new InvalidOperationException("Không tìm thấy LotStatus 'IN_STOCK'.");

            var datePart = now.ToString("yyyyMMdd");
            decimal totalActualRice = 0;

            foreach (var outputDto in dto.Outputs)
            {
                if (!outputDto.IsByproduct) totalActualRice += outputDto.OutputWeightKg;

                var lotType = outputDto.IsByproduct ? "BYPRODUCT" : "RICE";
                var baseCode = $"LOT-{lotType}-{datePart}";
                var count = await _paddyLotRepository.FindByCondition(x => x.LotCode.StartsWith(baseCode)).CountAsync();
                var lotCode = $"{baseCode}-{(count + 1):D4}";

                // #5: Retry chống trùng mã lô đầu ra
                int lotAttempts = 0;
                while (await _paddyLotRepository.FindByCondition(x => x.LotCode == lotCode).AnyAsync() && lotAttempts < 10)
                {
                    lotAttempts++;
                    lotCode = $"{baseCode}-{(count + 1 + lotAttempts):D4}";
                }

                var unitCost = outputDto.UnitCost ?? defaultUnitCost;

                var newLot = new PaddyLot
                {
                    OrganizationId = order.OrganizationId,
                    LotCode = lotCode,
                    LotType = lotType,
                    ProductVariantId = outputDto.ProductVariantId,
                    StatusId = defaultLotStatus.Id,
                    SourceMillingOrderId = orderId,
                    WarehouseId = order.WarehouseId,
                    LocationId = outputDto.LocationId,
                    InboundDate = now,
                    InitialWeightKg = outputDto.OutputWeightKg,
                    RemainingWeightKg = outputDto.OutputWeightKg,
                    CostPricePerKg = unitCost,
                    QrCode = "PL-" + Guid.NewGuid().ToString("N").ToUpper(),
                    CreatedBy = completedById,
                    CreatedDate = now
                };

                await _paddyLotRepository.CreateAsync(newLot);
                await _paddyLotRepository.SaveChangesAsync();

                var output = new MillingOrderOutput
                {
                    MillingOrderId = order.Id,
                    ProductVariantId = outputDto.ProductVariantId,
                    OutputLotId = newLot.Id,
                    LocationId = outputDto.LocationId,
                    OutputType = outputDto.OutputType.Trim().ToUpper(),
                    OutputWeightKg = outputDto.OutputWeightKg,
                    BagCount = outputDto.BagCount,
                    IsByproduct = outputDto.IsByproduct,
                    UnitCost = outputDto.UnitCost,
                    CreatedBy = completedById,
                    CreatedDate = now
                };
                await _outputRepository.CreateAsync(output);
                await _outputRepository.SaveChangesAsync();

                await ImportOutputInventoryAsync(newLot, output.OutputWeightKg, orderId, output.Id, completedById, now);
            }

            if (completedStatus != null) order.StatusId = completedStatus.Id;
            order.YieldRateUsed = dto.ActualYieldRate > 0 ? dto.ActualYieldRate : (totalConsumedKg > 0 ? totalActualRice / totalConsumedKg : 0);
            order.TotalRiceOutputKg = totalActualRice;
            order.ByproductKg = dto.ByproductKg;
            order.LossKg = dto.LossKg;
            order.MachineRef = dto.MachineRef;
            order.OperatorId = dto.OperatorId;
            order.CompletedAt = now;
            order.UpdatedBy = completedById;
            order.LastModifiedDate = now;

            await _millingOrderRepository.UpdateAsync(order);
            await _millingOrderRepository.SaveChangesAsync();

            await tx.CommitAsync();

            await _notificationDispatcher.DispatchAsync(
                NotificationConstants.Code.MillingCompleted,
                new NotificationTarget { RoleIds = new List<int> { CommonConstants.Role.OWNER, CommonConstants.Role.WAREHOUSE, CommonConstants.Role.SALES } },
                new object[] { order.MillingCode },
                "/admin/milling-orders",
                completedById);

            return ApiResponse.Success(new { OrderId = orderId }, "Hoàn thành lệnh xay.");
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync();
            return ApiResponse.UnprocessableEntity(ex.Message);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private async Task ImportOutputInventoryAsync(PaddyLot lot, decimal qty, int orderId, int outputId, int userId, DateTime now)
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
        // Áp dụng bình quân gia quyền cho giá vốn khi nhập thêm thành phẩm
        if (before > 0 && inventory.CostPrice > 0)
        {
            inventory.CostPrice = Math.Round(((before * inventory.CostPrice) + (qty * lot.CostPricePerKg)) / (before + qty), 2);
        }
        else
        {
            inventory.CostPrice = lot.CostPricePerKg;
        }

        inventory.QuantityOnHand += qty;
        inventory.LastModifiedDate = now;
        await _inventoryRepository.UpdateAsync(inventory);

        var tx = new InventoryTransaction
        {
            InventoryId = inventory.Id,
            WarehouseId = lot.WarehouseId,
            LocationId = lot.LocationId,
            ProductVariantId = lot.ProductVariantId,
            TransactionType = InventoryTransactionTypeConstants.Import,
            ReferenceType = InventoryReferenceTypeConstants.MillingOrder,
            ReferenceId = orderId,
            ReferenceItemId = outputId,
            Quantity = qty,
            BeforeQuantity = before,
            AfterQuantity = inventory.QuantityOnHand,
            WeightKg = qty,
            Note = $"Nhập gạo/phụ phẩm lô {lot.LotCode} từ lệnh xay",
            CreatedDate = now,
            CreatedBy = userId
        };

        await _inventoryTransactionRepository.CreateAsync(tx);
        await _inventoryTransactionRepository.SaveChangesAsync();
    }

    private static MillingOrderDetailDto ToDetailDto(MillingOrder x) => new()
    {
        Id = x.Id,
        OrganizationId = x.OrganizationId,
        MillingCode = x.MillingCode,
        StatusId = x.StatusId,
        StatusName = x.Status?.Name,
        WarehouseId = x.WarehouseId,
        WarehouseName = x.Warehouse?.Name,
        Reason = x.Reason,
        SalesOrderId = x.SalesOrderId,
        YieldRateUsed = x.YieldRateUsed,
        TotalRiceOutputKg = x.TotalRiceOutputKg,
        ComputedPaddyKg = x.ComputedPaddyKg,
        ByproductKg = x.ByproductKg,
        LossKg = x.LossKg,
        MachineRef = x.MachineRef,
        OperatorId = x.OperatorId,
        StartedAt = x.StartedAt,
        CompletedAt = x.CompletedAt,
        TotalCost = x.TotalCost,
        Inputs = x.MillingOrderInputs.Select(i => new MillingOrderInputDetailDto
        {
            Id = i.Id,
            PaddyLotId = i.PaddyLotId,
            LotCode = i.PaddyLot?.LotCode,
            LocationId = i.LocationId,
            ConsumedWeightKg = i.ConsumedWeightKg,
            ReservedWeightKg = i.ReservedWeightKg,
            Note = i.Note
        }).ToList(),
        Outputs = x.MillingOrderOutputs.Select(o => new MillingOrderOutputDetailDto
        {
            Id = o.Id,
            ProductVariantId = o.ProductVariantId,
            SKU = o.ProductVariant?.SKU,
            OutputLotId = o.OutputLotId,
            LocationId = o.LocationId,
            OutputType = o.OutputType,
            OutputWeightKg = o.OutputWeightKg,
            BagCount = o.BagCount,
            IsByproduct = o.IsByproduct,
            UnitCost = o.UnitCost
        }).ToList(),
        CreatedDate = x.CreatedDate,
        LastModifiedDate = x.LastModifiedDate
    };
}
