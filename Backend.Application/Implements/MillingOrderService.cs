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
    private readonly IMillingYieldConfigRepository _yieldConfigRepository;
    private readonly IRepositoryBase<Alert, int> _alertRepository;
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
        IMillingYieldConfigRepository yieldConfigRepository,
        IRepositoryBase<Alert, int> alertRepository,
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
        _yieldConfigRepository = yieldConfigRepository;
        _alertRepository = alertRepository;
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
        var stampedYield = await ResolveYieldRateAsync(
            obj.OrganizationId,
            obj.RiceVarietyId,
            obj.MoisturePercent,
            obj.ExpectedYield);

        // #17: Validate tỷ lệ thu hồi (yield) và khối lượng gạo mục tiêu
        if (stampedYield <= 0 || stampedYield > 1)
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

        var computedPaddyKg = obj.TargetRiceKg / stampedYield;

        var order = new MillingOrder
        {
            OrganizationId = obj.OrganizationId,
            MillingCode = millingCode,
            StatusId = draftStatus.Id,
            WarehouseId = obj.WarehouseId,
            Reason = obj.Reason?.Trim(),
            SalesOrderId = obj.SalesOrderId,
            YieldRateUsed = stampedYield,
            TotalRiceOutputKg = obj.TargetRiceKg,
            ComputedPaddyKg = computedPaddyKg,
            TotalCost = Math.Max(0, obj.MillingCost ?? 0) + Math.Max(0, obj.IncidentalCost ?? 0),
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
            .FindByCondition(x => !x.IsDeleted, true, x => x.Status, x => x.Warehouse)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        return ApiResponse.Success(entities.Select(x => ToDetailDto(x)).ToList());
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var entity = await _millingOrderRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted,
                true, // no-tracking (chỉ đọc để hiển thị)
                x => x.Status,
                x => x.Warehouse,
                x => x.MillingOrderInputs,
                x => x.MillingOrderOutputs)
            // Tách SELECT theo từng collection (Inputs × Outputs) tránh nổ tích Descartes.
            .AsSplitQuery()
            .FirstOrDefaultAsync();

        if (entity == null) return ApiResponse.NotFound();
        return ApiResponse.Success(ToDetailDto(entity));
    }

    /// <summary>Gap 2: Lấy các lệnh xay gắn với một đơn bán (điều phối xay-theo-đơn).</summary>
    public async Task<ApiResponse> GetBySalesOrderAsync(int salesOrderId)
    {
        var entities = await _millingOrderRepository
            .FindByCondition(x => x.SalesOrderId == salesOrderId && !x.IsDeleted, true, x => x.Status, x => x.Warehouse)
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

        var stampedYield = await ResolveYieldRateAsync(
            obj.OrganizationId,
            obj.RiceVarietyId,
            obj.MoisturePercent,
            obj.ExpectedYield);

        // #17: Validate tỷ lệ thu hồi (yield) và khối lượng gạo mục tiêu
        if (stampedYield <= 0 || stampedYield > 1)
            return ApiResponse.UnprocessableEntity("Tỷ lệ thu hồi (ExpectedYield) phải nằm trong khoảng (0, 1]. Ví dụ 0.65 = 65%.");
        if (obj.TargetRiceKg <= 0)
            return ApiResponse.UnprocessableEntity("Khối lượng gạo mục tiêu (TargetRiceKg) phải lớn hơn 0.");

        existData.OrganizationId = obj.OrganizationId;
        existData.WarehouseId = obj.WarehouseId;
        existData.Reason = obj.Reason?.Trim();
        existData.SalesOrderId = obj.SalesOrderId;
        existData.YieldRateUsed = stampedYield;
        existData.TotalRiceOutputKg = obj.TargetRiceKg;
        existData.ComputedPaddyKg = obj.TargetRiceKg / stampedYield;
        if (obj.MillingCost.HasValue || obj.IncidentalCost.HasValue)
            existData.TotalCost = Math.Max(0, obj.MillingCost ?? 0) + Math.Max(0, obj.IncidentalCost ?? 0);
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

        if (dto.Inputs == null || dto.Inputs.Count == 0)
            return ApiResponse.UnprocessableEntity("Vui lòng chọn ít nhất một lô/cột lúa đầu vào.");

        var duplicateSelections = dto.Inputs
            .GroupBy(x => new { x.PaddyLotId, x.LocationId })
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateSelections != null)
            return ApiResponse.UnprocessableEntity("Không được chọn trùng cùng một lô tại cùng vị trí/cột.");

        var totalRequestedKg = dto.Inputs.Sum(x => x.ReservedWeightKg ?? x.ConsumedWeightKg);
        if (Math.Abs(totalRequestedKg - order.ComputedPaddyKg) > 0.01m)
            return ApiResponse.UnprocessableEntity(
                $"Tổng lượng giữ phải bằng lượng lúa dự kiến {order.ComputedPaddyKg:N3} kg.");

        var now = DateTimeHelper.VietnamNow();

        await using var tx = await _millingOrderRepository.BeginTransactionAsync();
        try
        {
            foreach (var inputDto in dto.Inputs)
            {
                var lot = await _paddyLotRepository.GetByIdAsync(inputDto.PaddyLotId);
                if (lot == null) return ApiResponse.Error($"Không tìm thấy lô lúa {inputDto.PaddyLotId}", 404);
                if (!string.Equals(lot.LotType, "PADDY", StringComparison.OrdinalIgnoreCase))
                    return ApiResponse.UnprocessableEntity($"Lô {lot.LotCode} không phải lô lúa nguyên liệu.");
                if (lot.WarehouseId != order.WarehouseId)
                    return ApiResponse.UnprocessableEntity($"Lô {lot.LotCode} không thuộc kho thực hiện lệnh xay.");

                // #2/#11: Chặn đưa lô đang CÁCH LY / không đủ điều kiện vào xay
                if (await IsLotBlockedForMillingAsync(lot.StatusId))
                    return ApiResponse.UnprocessableEntity($"Lô {lot.LotCode} đang bị cách ly hoặc không đủ điều kiện để đưa vào xay xát.");

                var requestedWeightKg = inputDto.ReservedWeightKg ?? inputDto.ConsumedWeightKg;
                if (requestedWeightKg <= 0)
                    return ApiResponse.UnprocessableEntity($"Khối lượng giữ của lô {lot.LotCode} phải lớn hơn 0.");

                if (lot.RemainingWeightKg < requestedWeightKg)
                    return ApiResponse.UnprocessableEntity($"Lô {lot.LotCode} không đủ lúa (còn {lot.RemainingWeightKg} kg, yêu cầu {requestedWeightKg} kg).");

                var targetLocationId = inputDto.LocationId ?? lot.LocationId;
                var inventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                    lot.ProductVariantId, lot.WarehouseId, targetLocationId, lot.Id);

                if (inventory == null || inventory.QuantityOnHand - inventory.QuantityReserved < requestedWeightKg)
                    return ApiResponse.UnprocessableEntity($"Lô {lot.LotCode} không đủ tồn kho khả dụng để giữ.");

                // Hold inventory
                inventory.QuantityReserved += requestedWeightKg;
                inventory.LastModifiedDate = now;
                await _inventoryRepository.UpdateAsync(inventory);

                // Create MillingOrderInput
                var input = new MillingOrderInput
                {
                    MillingOrderId = order.Id,
                    PaddyLotId = inputDto.PaddyLotId,
                    LocationId = inputDto.LocationId,
                    // Chưa có cân đầu vào. Lượng tiêu thụ chỉ được ghi ở bước
                    // complete sau khi tính gạo thực tế / YieldRateUsed.
                    ConsumedWeightKg = 0,
                    ReservedWeightKg = requestedWeightKg,
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
        if (currentCode != LookupCodes.MillingOrderStatus.Draft &&
            currentCode != LookupCodes.MillingOrderStatus.Reserved)
            return ApiResponse.UnprocessableEntity($"Không thể hủy lệnh xay ở trạng thái {currentCode}.");

        var cancelStatus = await _statusRepository.FirstOrDefaultAsync(x => x.Code == LookupCodes.MillingOrderStatus.Cancelled && !x.IsDeleted)
            ?? throw new InvalidOperationException("Không tìm thấy trạng thái CANCELLED.");

        var now = DateTimeHelper.VietnamNow();

        await using var tx = await _millingOrderRepository.BeginTransactionAsync();
        try
        {
            if (currentCode == LookupCodes.MillingOrderStatus.Reserved)
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

        if (order.Status?.Code != LookupCodes.MillingOrderStatus.InProgress)
            return ApiResponse.UnprocessableEntity("Chỉ có thể hoàn thành lệnh xay ở trạng thái Đang xay.");
        if (order.YieldRateUsed <= 0 || order.YieldRateUsed > 1)
            return ApiResponse.UnprocessableEntity("Lệnh xay không có YieldRateUsed hợp lệ.");
        if (order.MillingOrderInputs.Count == 0)
            return ApiResponse.UnprocessableEntity("Lệnh xay chưa có lô/cột lúa đầu vào.");
        if (dto.Outputs == null || dto.Outputs.Count == 0)
            return ApiResponse.UnprocessableEntity("Vui lòng khai báo ít nhất một dòng đầu ra.");

        var completedStatus = await _statusRepository.FirstOrDefaultAsync(x => x.Code == LookupCodes.MillingOrderStatus.Completed && !x.IsDeleted);
        if (completedStatus == null)
            throw new InvalidOperationException("Không tìm thấy trạng thái COMPLETED.");

        var now = DateTimeHelper.VietnamNow();
        var allowedOutputTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "RICE", "BROKEN", "BRAN", "HUSK"
        };

        foreach (var output in dto.Outputs)
        {
            var outputType = output.OutputType?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(outputType) || !allowedOutputTypes.Contains(outputType))
                return ApiResponse.UnprocessableEntity($"Loại đầu ra '{output.OutputType}' không hợp lệ.");
            if (output.ProductVariantId <= 0)
                return ApiResponse.UnprocessableEntity("Mỗi dòng đầu ra phải chọn một SKU.");
            if (!output.LocationId.HasValue)
                return ApiResponse.UnprocessableEntity("Mỗi dòng đầu ra phải chọn vị trí nhập kho.");
            if (output.OutputWeightKg <= 0)
                return ApiResponse.UnprocessableEntity("Khối lượng mỗi dòng đầu ra phải lớn hơn 0.");
        }

        var totalActualRice = dto.Outputs
            .Where(x => string.Equals(x.OutputType?.Trim(), "RICE", StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.OutputWeightKg);
        if (totalActualRice <= 0)
            return ApiResponse.UnprocessableEntity("Phải có ít nhất một dòng gạo thành phẩm với khối lượng lớn hơn 0.");

        var computedPaddyKg = Math.Round(
            totalActualRice / order.YieldRateUsed,
            3,
            MidpointRounding.AwayFromZero);
        var totalProducedKg = dto.Outputs.Sum(x => x.OutputWeightKg);
        var lossKg = dto.LossKg ?? 0;
        if (lossKg < 0)
            return ApiResponse.UnprocessableEntity("Khối lượng hao hụt không được âm.");

        var massBalanceTolerance = computedPaddyKg * 0.02m;
        if ((totalProducedKg + lossKg) > (computedPaddyKg + massBalanceTolerance))
        {
            return ApiResponse.UnprocessableEntity(
                $"Mass balance không hợp lệ: Tổng đầu ra ({totalProducedKg} kg) + Hao hụt ({lossKg} kg) " +
                $"vượt quá lượng lúa tính ngược ({computedPaddyKg} kg) + 2% dung sai.");
        }

        var expectedRiceKg = order.TotalRiceOutputKg;
        var outputDeviationPercent = expectedRiceKg > 0
            ? Math.Abs((totalActualRice - expectedRiceKg) / expectedRiceKg) * 100
            : 0;
        if (outputDeviationPercent > 2 && string.IsNullOrWhiteSpace(dto.Note))
            return ApiResponse.UnprocessableEntity(
                $"Sản lượng gạo thực tế lệch kế hoạch {outputDeviationPercent:N2}%. Vui lòng nhập lý do sai lệch.");

        await using var tx = await _millingOrderRepository.BeginTransactionAsync();
        try
        {
            var inputStates = new List<(MillingOrderInput Input, PaddyLot Lot, Inventory Inventory, decimal OwnReserved, decimal MaxConsumable)>();
            var duplicateInputs = new HashSet<string>();

            foreach (var input in order.MillingOrderInputs.OrderBy(x => x.CreatedDate).ThenBy(x => x.Id))
            {
                var lot = await _paddyLotRepository.GetByIdAsync(input.PaddyLotId);
                if (lot == null)
                    throw new InvalidOperationException($"Không tìm thấy lô lúa Id={input.PaddyLotId}.");
                if (await IsLotBlockedForMillingAsync(lot.StatusId))
                    throw new InvalidOperationException($"Lô {lot.LotCode} đang bị cách ly hoặc không đủ điều kiện để xay xát.");

                var targetLocationId = input.LocationId ?? lot.LocationId;
                if (!targetLocationId.HasValue)
                    throw new InvalidOperationException($"Lô {lot.LotCode} chưa có vị trí/cột đầu vào.");

                var duplicateKey = $"{lot.Id}:{targetLocationId.Value}";
                if (!duplicateInputs.Add(duplicateKey))
                    throw new InvalidOperationException($"Lô {lot.LotCode} tại cùng vị trí đang bị khai báo trùng.");

                var inventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                    lot.ProductVariantId, lot.WarehouseId, targetLocationId, lot.Id);
                if (inventory == null)
                    throw new InvalidOperationException($"Không tìm thấy tồn kho của lô {lot.LotCode} tại vị trí đã giữ.");

                var ownReserved = input.ReservedWeightKg.GetValueOrDefault();
                var reservedByOtherOrders = Math.Max(0, inventory.QuantityReserved - ownReserved);
                var maxConsumable = Math.Max(
                    0,
                    Math.Min(lot.RemainingWeightKg, inventory.QuantityOnHand - reservedByOtherOrders));

                inputStates.Add((input, lot, inventory, ownReserved, maxConsumable));
            }

            var allocations = inputStates.ToDictionary(x => x.Input, _ => 0m);
            var remainingPaddyKg = computedPaddyKg;

            // Dùng phần đã giữ trước.
            foreach (var state in inputStates)
            {
                var consume = Math.Min(remainingPaddyKg, Math.Min(state.OwnReserved, state.MaxConsumable));
                allocations[state.Input] += consume;
                remainingPaddyKg -= consume;
                if (remainingPaddyKg <= 0.0005m) break;
            }

            // Nếu sản lượng thực tế cao hơn kế hoạch, chỉ lấy thêm phần còn khả dụng
            // trong chính các lô/cột đã chọn; không đụng vào lượng của đơn khác.
            if (remainingPaddyKg > 0.0005m)
            {
                foreach (var state in inputStates)
                {
                    var extraCapacity = Math.Max(0, state.MaxConsumable - allocations[state.Input]);
                    var consume = Math.Min(remainingPaddyKg, extraCapacity);
                    allocations[state.Input] += consume;
                    remainingPaddyKg -= consume;
                    if (remainingPaddyKg <= 0.0005m) break;
                }
            }

            if (remainingPaddyKg > 0.0005m)
                throw new InvalidOperationException(
                    $"Các lô/cột đã chọn không đủ lúa khả dụng. Còn thiếu {remainingPaddyKg:N3} kg.");

            decimal totalMaterialCost = 0;
            foreach (var state in inputStates)
            {
                var consumedKg = allocations[state.Input];
                state.Input.ConsumedWeightKg = consumedKg;
                state.Input.UpdatedBy = completedById;
                state.Input.LastModifiedDate = now;
                await _inputRepository.UpdateAsync(state.Input);

                state.Lot.RemainingWeightKg -= consumedKg;
                state.Lot.LastModifiedDate = now;
                await _paddyLotRepository.UpdateAsync(state.Lot);

                totalMaterialCost += consumedKg * state.Lot.CostPricePerKg;

                var before = state.Inventory.QuantityOnHand;
                state.Inventory.QuantityOnHand -= consumedKg;
                state.Inventory.QuantityReserved = Math.Max(
                    0,
                    state.Inventory.QuantityReserved - state.OwnReserved);
                state.Inventory.LastModifiedDate = now;
                await _inventoryRepository.UpdateAsync(state.Inventory);

                var invTx = new InventoryTransaction
                {
                    InventoryId = state.Inventory.Id,
                    WarehouseId = state.Lot.WarehouseId,
                    LocationId = state.Input.LocationId ?? state.Lot.LocationId,
                    ProductVariantId = state.Lot.ProductVariantId,
                    TransactionType = InventoryTransactionTypeConstants.Export,
                    ReferenceType = InventoryReferenceTypeConstants.MillingOrder,
                    ReferenceId = orderId,
                    ReferenceItemId = state.Input.Id,
                    Quantity = -consumedKg,
                    BeforeQuantity = before,
                    AfterQuantity = state.Inventory.QuantityOnHand,
                    WeightKg = consumedKg,
                    Note = $"Xay lúa lô {state.Lot.LotCode}; tính ngược theo yield {order.YieldRateUsed:P2}",
                    CreatedDate = now,
                    CreatedBy = completedById
                };
                await _inventoryTransactionRepository.CreateWithColumnTotalsAsync(invTx);
            }

            await _paddyLotRepository.SaveChangesAsync();

            var operatingCost =
                dto.MillingCost.HasValue || dto.IncidentalCost.HasValue
                    ? Math.Max(0, dto.MillingCost ?? 0) + Math.Max(0, dto.IncidentalCost ?? 0)
                    : Math.Max(0, order.TotalCost ?? 0);
            decimal totalCostToAllocate = totalMaterialCost + operatingCost;
            decimal defaultUnitCost = totalProducedKg > 0
                ? totalCostToAllocate / totalProducedKg
                : 0;

            var defaultLotStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == LotStatusCodeConstants.InStock && !x.IsDeleted)
                ?? throw new InvalidOperationException("Không tìm thấy LotStatus 'IN_STOCK'.");

            var datePart = now.ToString("yyyyMMdd");

            foreach (var outputDto in dto.Outputs)
            {
                var outputType = outputDto.OutputType.Trim().ToUpperInvariant();
                var isByproduct = outputType != "RICE";
                var locationUpdated = await _locationRepository.UpdateCapacitySafetyAsync(
                    outputDto.LocationId!.Value,
                    order.WarehouseId,
                    outputDto.OutputWeightKg,
                    outputDto.ProductVariantId,
                    false,
                    completedById);
                if (locationUpdated == 0)
                    throw new InvalidOperationException(
                        $"Vị trí #{outputDto.LocationId} không hợp lệ, khác kho, khác loại hàng hoặc không đủ sức chứa.");

                var lotType = isByproduct ? "BYPRODUCT" : "RICE";
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

                // Giá vốn luôn do backend phân bổ; không tin đơn giá tự nhập từ client.
                var unitCost = defaultUnitCost;

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
                    OutputType = outputType,
                    OutputWeightKg = outputDto.OutputWeightKg,
                    BagCount = outputDto.BagCount,
                    IsByproduct = isByproduct,
                    UnitCost = unitCost,
                    CreatedBy = completedById,
                    CreatedDate = now
                };
                await _outputRepository.CreateAsync(output);
                await _outputRepository.SaveChangesAsync();

                await ImportOutputInventoryAsync(newLot, output.OutputWeightKg, orderId, output.Id, completedById, now);
            }

            order.StatusId = completedStatus.Id;
            order.TotalRiceOutputKg = totalActualRice;
            order.ComputedPaddyKg = computedPaddyKg;
            order.ByproductKg = dto.Outputs
                .Where(x => !string.Equals(x.OutputType?.Trim(), "RICE", StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.OutputWeightKg);
            order.LossKg = lossKg;
            order.TotalCost = totalCostToAllocate;
            order.MachineRef = dto.MachineRef?.Trim();
            order.OperatorId = dto.OperatorId;
            order.CompletedAt = now;
            order.UpdatedBy = completedById;
            order.LastModifiedDate = now;

            await _millingOrderRepository.UpdateAsync(order);

            if (outputDeviationPercent > 2)
            {
                await _alertRepository.CreateAsync(new Alert
                {
                    AlertType = "MILLING_YIELD_DEVIATION",
                    Severity = outputDeviationPercent >= 10 ? "CRITICAL" : "WARNING",
                    WarehouseId = order.WarehouseId,
                    Message =
                        $"Lệnh {order.MillingCode}: gạo thực tế {totalActualRice:N3} kg lệch " +
                        $"{outputDeviationPercent:N2}% so với kế hoạch {expectedRiceKg:N3} kg. " +
                        $"Lý do: {dto.Note?.Trim()}",
                    RelatedEntityType = "MILLING_ORDER",
                    RelatedEntityId = order.Id,
                    Status = "OPEN",
                    DeduplicationKey = $"MILLING_YIELD_DEVIATION:{order.Id}",
                    CreatedBy = completedById,
                    CreatedDate = now
                });
            }

            await _millingOrderRepository.SaveChangesAsync();

            await tx.CommitAsync();

            await _notificationDispatcher.DispatchAsync(
                NotificationConstants.Code.MillingCompleted,
                new NotificationTarget { RoleIds = new List<int> { CommonConstants.Role.OWNER, CommonConstants.Role.WAREHOUSE, CommonConstants.Role.SALES } },
                new object[] { order.MillingCode },
                "/admin/milling-orders",
                completedById);

            return ApiResponse.Success(
                new
                {
                    OrderId = orderId,
                    RiceOutputKg = totalActualRice,
                    ComputedPaddyKg = computedPaddyKg,
                    YieldRateUsed = order.YieldRateUsed
                },
                "Hoàn thành lệnh xay, trừ lúa tính ngược và nhập kho thành phẩm.");
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

    private async Task<decimal> ResolveYieldRateAsync(
        int? organizationId,
        int? riceVarietyId,
        decimal? moisturePercent,
        decimal fallbackYield)
    {
        var now = DateTimeHelper.VietnamNow();
        var query = _yieldConfigRepository
            .FindByCondition(x =>
                !x.IsDeleted &&
                x.IsActive &&
                (!x.EffectiveFrom.HasValue || x.EffectiveFrom <= now) &&
                (!organizationId.HasValue || !x.OrganizationId.HasValue || x.OrganizationId == organizationId) &&
                (!riceVarietyId.HasValue || !x.RiceVarietyId.HasValue || x.RiceVarietyId == riceVarietyId));

        if (moisturePercent.HasValue)
        {
            query = query.Where(x =>
                (!x.MoistureFrom.HasValue || x.MoistureFrom <= moisturePercent) &&
                (!x.MoistureTo.HasValue || x.MoistureTo >= moisturePercent));
        }

        var config = await query
            .OrderByDescending(x => riceVarietyId.HasValue && x.RiceVarietyId == riceVarietyId)
            .ThenByDescending(x => organizationId.HasValue && x.OrganizationId == organizationId)
            .ThenByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync();

        return config?.YieldRate ?? fallbackYield;
    }

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

        await _inventoryTransactionRepository.CreateWithColumnTotalsAsync(tx);
        await _inventoryTransactionRepository.SaveChangesAsync();
    }

    private static MillingOrderDetailDto ToDetailDto(MillingOrder x) => new()
    {
        Id = x.Id,
        OrganizationId = x.OrganizationId,
        MillingCode = x.MillingCode,
        StatusId = x.StatusId,
        StatusName = x.Status?.Name,
        StatusCode = x.Status?.Code,
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
        MillingCost = x.Status?.Code == LookupCodes.MillingOrderStatus.Completed ? null : x.TotalCost,
        IncidentalCost = null,
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
