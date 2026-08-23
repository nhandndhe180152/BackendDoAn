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
    private readonly IRepositoryBase<PaddyLotBag, int>? _bagRepository;
    private readonly IRepositoryBase<PaddyLotBagContent, int>? _bagContentRepository;
    private readonly IRepositoryBase<PaddyLotBagAllocation, int>? _bagAllocationRepository;
    private readonly IRepositoryBase<PaddyLotBagMovement, int>? _bagMovementRepository;
    private readonly ISystemConfigRepository? _systemConfigRepository;
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IUserRepository _userRepository;

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
        INotificationDispatcher notificationDispatcher,
        ISalesOrderRepository salesOrderRepository,
        IUserRepository userRepository,
        IRepositoryBase<PaddyLotBag, int>? bagRepository = null,
        IRepositoryBase<PaddyLotBagContent, int>? bagContentRepository = null,
        ISystemConfigRepository? systemConfigRepository = null,
        IRepositoryBase<PaddyLotBagAllocation, int>? bagAllocationRepository = null,
        IRepositoryBase<PaddyLotBagMovement, int>? bagMovementRepository = null)
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
        _salesOrderRepository = salesOrderRepository;
        _userRepository = userRepository;
        _bagRepository = bagRepository;
        _bagContentRepository = bagContentRepository;
        _systemConfigRepository = systemConfigRepository;
        _bagAllocationRepository = bagAllocationRepository;
        _bagMovementRepository = bagMovementRepository;
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

    private IQueryable<User> EligibleMillingOperators()
        => _userRepository.GetAll()
            .AsNoTracking()
            .Where(user =>
                !user.IsDeleted &&
                user.UserStatus.Code == LookupCodes.UserStatus.Active &&
                user.UserRoles.Any(userRole =>
                    !userRole.IsDeleted &&
                    !userRole.Role.IsDeleted &&
                    userRole.Role.Code == LookupCodes.Role.Milling));

    private Task<bool> IsEligibleMillingOperatorAsync(int operatorId)
        => EligibleMillingOperators().AnyAsync(user => user.Id == operatorId);

    public async Task<ApiResponse> CreateAsync(CreateMillingOrderDto obj)
    {
        if (obj.SalesOrderId.HasValue)
        {
            var salesOrder = await _salesOrderRepository.GetByIdDetailAsync(obj.SalesOrderId.Value);
            if (salesOrder == null)
                return ApiResponse.UnprocessableEntity("Không tìm thấy đơn bán đã chọn.");
            if (!salesOrder.RequiresMilling)
                return ApiResponse.UnprocessableEntity("Đơn bán đã chọn không được đánh dấu là cần xay.");

            var riceItems = salesOrder.SalesOrderItems
                .Where(i => !i.IsDeleted && !i.ProductVariant.IsByproduct)
                .ToList();
            if (riceItems.Count == 0)
                return ApiResponse.UnprocessableEntity("Đơn bán không có sản phẩm gạo chính để xác định giống lúa.");
            if (riceItems.Any(i => !i.ProductVariant.RiceVarietyId.HasValue))
                return ApiResponse.UnprocessableEntity("Có sản phẩm trong đơn bán chưa được cấu hình giống lúa. Vui lòng cập nhật biến thể sản phẩm trước khi tạo lệnh xay.");

            var varietyIds = riceItems
                .Select(i => i.ProductVariant.RiceVarietyId!.Value)
                .Distinct()
                .ToList();
            if (varietyIds.Count != 1)
                return ApiResponse.UnprocessableEntity("Đơn bán có nhiều giống lúa. Vui lòng tách đơn theo từng giống trước khi tạo lệnh xay.");
            if (obj.RiceVarietyId.HasValue && obj.RiceVarietyId.Value != varietyIds[0])
                return ApiResponse.UnprocessableEntity("Giống lúa trên lệnh xay không khớp với sản phẩm của đơn bán.");

            var totalRiceRequiredKg = riceItems.Sum(i => i.QuantityOrdered);
            var allocatedMillingRiceKg = salesOrder.MillingOrders
                .Where(o => !o.IsDeleted && o.Status?.Code != "CANCELLED")
                .Sum(o => o.TotalRiceOutputKg);
            var remainingMillingRiceKg = Math.Max(0, totalRiceRequiredKg - allocatedMillingRiceKg);
            if (remainingMillingRiceKg <= 0)
                return ApiResponse.UnprocessableEntity("Đơn bán đã được phân bổ đủ khối lượng gạo cho các lệnh xay chưa hủy.");
            if (obj.TargetRiceKg > remainingMillingRiceKg + 0.001m)
                return ApiResponse.UnprocessableEntity(
                    $"Khối lượng gạo dự kiến vượt nhu cầu còn lại của đơn bán. Còn cần {remainingMillingRiceKg:0.###} kg.");

            obj.RiceVarietyId = varietyIds[0];
            obj.WarehouseId = salesOrder.WarehouseId ?? obj.WarehouseId;
            obj.OrganizationId ??= salesOrder.OrganizationId;
        }

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
            RiceVarietyId = obj.RiceVarietyId,
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
            .FindByCondition(x => !x.IsDeleted, true, x => x.Status, x => x.Warehouse, x => x.RiceVariety)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        return ApiResponse.Success(entities.Select(x => ToDetailDto(x)).ToList());
    }

    public async Task<ApiResponse> GetOperatorsAsync()
    {
        var operators = await EligibleMillingOperators()
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .Select(x => new
            {
                x.Id,
                x.FirstName,
                x.LastName
            })
            .ToListAsync();

        return ApiResponse.Success(operators.Select(x => new DataItem<int>
        {
            Id = x.Id,
            Name = $"{x.LastName} {x.FirstName}".Trim()
        }).ToList());
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var entity = await _millingOrderRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted,
                true, // no-tracking (chỉ đọc để hiển thị)
                x => x.Status,
                x => x.Warehouse,
                x => x.RiceVariety,
                x => x.MillingOrderInputs,
                x => x.MillingOrderOutputs)
            .Include(x => x.MillingOrderInputs).ThenInclude(x => x.PaddyLot)
            .Include(x => x.MillingOrderInputs).ThenInclude(x => x.Location)
            // Tách SELECT theo từng collection (Inputs × Outputs) tránh nổ tích Descartes.
            .AsSplitQuery()
            .FirstOrDefaultAsync();

        if (entity == null) return ApiResponse.NotFound();
        var dto = ToDetailDto(entity);
        if (_bagAllocationRepository != null)
        {
            var allocations = await _bagAllocationRepository
                .FindByCondition(x => x.ReferenceType == PaddyLotBagAllocationReferenceTypes.MillingOrder &&
                    x.ReferenceId == entity.Id && !x.IsDeleted, false)
                .Include(x => x.Bag).ToListAsync();
            foreach (var input in dto.Inputs)
                input.Bags = allocations.Where(x => x.ReferenceItemId == input.Id)
                    .OrderByDescending(x => x.StackOrderSnapshot)
                    .Select(x => new MillingSelectedBagDto
                    {
                        BagId = x.BagId,
                        BagNo = x.Bag.BagNo,
                        WeightKg = x.BagWeightSnapshotKg,
                        StackOrder = x.StackOrderSnapshot,
                        Status = x.Status
                    }).ToList();
        }
        return ApiResponse.Success(dto);
    }

    /// <summary>Gap 2: Lấy các lệnh xay gắn với một đơn bán (điều phối xay-theo-đơn).</summary>
    public async Task<ApiResponse> GetBySalesOrderAsync(int salesOrderId)
    {
        var entities = await _millingOrderRepository
            .FindByCondition(x => x.SalesOrderId == salesOrderId && !x.IsDeleted, true, x => x.Status, x => x.Warehouse, x => x.RiceVariety)
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

        if (existData.SalesOrderId.HasValue)
        {
            var salesOrder = await _salesOrderRepository.GetByIdDetailAsync(existData.SalesOrderId.Value);
            if (salesOrder == null)
                return ApiResponse.UnprocessableEntity("Không tìm thấy đơn bán gắn với lệnh xay.");

            var riceItems = salesOrder.SalesOrderItems
                .Where(i => !i.IsDeleted && !i.ProductVariant.IsByproduct)
                .ToList();
            if (riceItems.Count == 0 || riceItems.Any(i => !i.ProductVariant.RiceVarietyId.HasValue))
                return ApiResponse.UnprocessableEntity("Không thể xác định duy nhất giống lúa từ sản phẩm của đơn bán.");

            var varietyIds = riceItems
                .Select(i => i.ProductVariant.RiceVarietyId!.Value)
                .Distinct()
                .ToList();
            if (varietyIds.Count != 1)
                return ApiResponse.UnprocessableEntity("Đơn bán có nhiều giống lúa. Vui lòng tách đơn theo từng giống trước khi cập nhật lệnh xay.");
            if (obj.RiceVarietyId.HasValue && obj.RiceVarietyId.Value != varietyIds[0])
                return ApiResponse.UnprocessableEntity("Giống lúa trên lệnh xay không khớp với sản phẩm của đơn bán.");

            obj.RiceVarietyId = varietyIds[0];
            obj.SalesOrderId = existData.SalesOrderId;
            obj.WarehouseId = salesOrder.WarehouseId ?? obj.WarehouseId;
            obj.OrganizationId ??= salesOrder.OrganizationId;
        }

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
        existData.RiceVarietyId = obj.RiceVarietyId;
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
            .FindByCondition(x => x.Id == id && !x.IsDeleted, false, x => x.Status, x => x.MillingOrderInputs)
            .FirstOrDefaultAsync();

        if (order == null) return ApiResponse.Error("Không tìm thấy lệnh xay.", 404);
        if (order.Status?.Code == LookupCodes.MillingOrderStatus.InProgress)
            return ApiResponse.UnprocessableEntity("Lệnh đang xay đã khóa nguồn lúa. Chỉ được xem các bao đã giữ, không thể điều chỉnh nguồn.");
        var isInProgressReallocation = false;
        var isReallocation = order.Status?.Code == LookupCodes.MillingOrderStatus.Reserved;
        if (order.Status?.Code != LookupCodes.MillingOrderStatus.Draft && !isReallocation)
            return ApiResponse.UnprocessableEntity("Chỉ có thể giữ hoặc điều chỉnh nguồn lúa khi lệnh xay ở trạng thái Nháp/Đã giữ/Đang xay.");

        if (dto.Columns.Count > 0)
            return await ReservePhysicalBagsAsync(order, dto.Columns, userId, isReallocation, isInProgressReallocation);
        if (dto.Inputs.Count > 0)
            return ApiResponse.UnprocessableEntity(
                "Lệnh xay mới bắt buộc chọn nguyên bao bằng Columns/BagIds; không còn hỗ trợ nhập số kg tùy ý.");

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
            if (isReallocation)
            {
                foreach (var oldInput in order.MillingOrderInputs.Where(x => !x.IsDeleted))
                {
                    var oldLot = await _paddyLotRepository.GetByIdAsync(oldInput.PaddyLotId);
                    if (oldLot != null)
                    {
                        var oldLocationId = oldInput.LocationId ?? oldLot.LocationId;
                        var oldInventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                            oldLot.ProductVariantId, oldLot.WarehouseId, oldLocationId, oldLot.Id);
                        if (oldInventory != null)
                        {
                            oldInventory.QuantityReserved = Math.Max(
                                0,
                                oldInventory.QuantityReserved - oldInput.ReservedWeightKg.GetValueOrDefault());
                            oldInventory.LastModifiedDate = now;
                            await _inventoryRepository.UpdateAsync(oldInventory);
                        }
                    }
                    await _inputRepository.SoftDeleteAsync(oldInput.Id);
                }
            }

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
                if (!targetLocationId.HasValue)
                    return ApiResponse.UnprocessableEntity($"Lô {lot.LotCode} chưa có vị trí/cột để lấy lúa.");

                var retrieval = await GetBagRetrievalStateAsync(lot.Id, targetLocationId.Value, requestedWeightKg);
                if (retrieval.HasPhysicalBags && !retrieval.CanRetrieve)
                    return ApiResponse.UnprocessableEntity(BuildBagRetrievalError(lot.LotCode, targetLocationId.Value, requestedWeightKg, retrieval));

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

            if (!isInProgressReallocation)
                order.StatusId = reservedStatus.Id;
            order.UpdatedBy = userId;
            order.LastModifiedDate = now;
            await _millingOrderRepository.UpdateAsync(order);

            await _millingOrderRepository.SaveChangesAsync();
            await tx.CommitAsync();

            return ApiResponse.Success(order.Id, isReallocation
                ? (isInProgressReallocation ? "Điều chỉnh nguồn lúa đang xay thành công." : "Phân bổ lại lúa thành công.")
                : "Giữ lúa thành công.");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task<ApiResponse> ReservePhysicalBagsAsync(
        MillingOrder order,
        IReadOnlyCollection<MillingBagSelectionColumnDto> columns,
        int userId,
        bool isReallocation,
        bool isInProgressReallocation)
    {
        if (_bagRepository == null || _bagAllocationRepository == null)
            return ApiResponse.Error("Chưa cấu hình kho lưu bao vật lý.", 500);
        if (columns.Any(x => x.LocationId <= 0 || x.BagIds.Count == 0))
            return ApiResponse.UnprocessableEntity("Mỗi cột phải có vị trí và ít nhất một bao.");
        if (columns.GroupBy(x => x.LocationId).Any(x => x.Count() > 1))
            return ApiResponse.UnprocessableEntity("Mỗi vị trí/cột chỉ được khai báo một lần.");
        var requestedIds = columns.SelectMany(x => x.BagIds).ToList();
        if (requestedIds.Distinct().Count() != requestedIds.Count)
            return ApiResponse.UnprocessableEntity("Một bao không được chọn lặp lại.");

        var requestedLocationIds = columns.Select(x => x.LocationId).Distinct().ToList();
        var lockedLocation = await _locationRepository.FindByCondition(x =>
                requestedLocationIds.Contains(x.Id) && x.OutboundLockOrderId.HasValue && !x.IsDeleted, false)
            .FirstOrDefaultAsync();
        if (lockedLocation != null)
            return ApiResponse.Conflict(
                $"Cột '{lockedLocation.SlotCode ?? $"#{lockedLocation.Id}"}' đang được khóa để lấy hàng cho phiếu xuất #{lockedLocation.OutboundLockOrderId}. Vui lòng chọn cột khác.");

        var selectedBags = new List<PaddyLotBag>();
        var selectedByColumn = new Dictionary<int, List<PaddyLotBag>>();
        foreach (var column in columns)
        {
            var stack = await _bagRepository
                .FindByCondition(x => x.LocationId == column.LocationId && x.Status == PaddyLotBagStatuses.Stored && !x.IsDeleted, false)
                .Include(x => x.Lot).ThenInclude(x => x.Status)
                .Include(x => x.Allocations)
                .OrderByDescending(x => x.StackOrder).ThenByDescending(x => x.Id)
                .ToListAsync();
            var requestedSequence = column.BagIds.ToList();
            var expectedTopSequence = stack.Take(requestedSequence.Count).Select(x => x.Id).ToList();
            if (!requestedSequence.SequenceEqual(expectedTopSequence))
                return ApiResponse.UnprocessableEntity(
                    $"Các bao tại vị trí #{column.LocationId} phải được chọn liên tục từ đỉnh cột xuống, đúng thứ tự hiển thị.");

            foreach (var bag in stack.Take(requestedSequence.Count))
            {
                if (bag.Lot.WarehouseId != order.WarehouseId || !string.Equals(bag.Lot.LotType, "PADDY", StringComparison.OrdinalIgnoreCase))
                    return ApiResponse.UnprocessableEntity($"Bao #{bag.BagNo} không phải lúa nguyên liệu trong kho thực hiện.");
                if (bag.Lot.Status.Code == LotStatusCodeConstants.Quarantine)
                    return ApiResponse.UnprocessableEntity($"Bao #{bag.BagNo} thuộc lô đang cách ly.");
                var heldByOther = bag.Allocations.Any(x => !x.IsDeleted && x.Status == PaddyLotBagAllocationStatuses.Active &&
                    !(x.ReferenceType == PaddyLotBagAllocationReferenceTypes.MillingOrder && x.ReferenceId == order.Id));
                if (heldByOther)
                    return ApiResponse.Conflict($"Bao #{bag.BagNo} vừa được chứng từ khác giữ. Vui lòng tải lại danh sách bao.");
                if (bag.WeightKg <= 0.0005m)
                    return ApiResponse.UnprocessableEntity($"Bao #{bag.BagNo} không còn khối lượng khả dụng.");
                selectedBags.Add(bag);
            }
            selectedByColumn[column.LocationId] = stack.Take(requestedSequence.Count).ToList();
        }

        var selectedWeightKg = selectedBags.Sum(x => x.WeightKg);
        if (selectedWeightKg + 0.0005m < order.ComputedPaddyKg)
            return ApiResponse.UnprocessableEntity(
                $"Tổng khối lượng các bao đã chọn ({selectedWeightKg:N3} kg) chưa đủ lượng lúa cần xay ({order.ComputedPaddyKg:N3} kg). Còn thiếu {order.ComputedPaddyKg - selectedWeightKg:N3} kg.");

        // Danh sách phải tối thiểu theo nguyên bao. Bao cuối (sâu nhất) của bất kỳ cột nào
        // mà có thể bỏ đi nhưng tổng vẫn đủ chính là một bao được giữ dư.
        var removableBottomBag = selectedByColumn.Values
            .Where(x => x.Count > 0)
            .Select(x => x[^1])
            .FirstOrDefault(x => selectedWeightKg - x.WeightKg + 0.0005m >= order.ComputedPaddyKg);
        if (removableBottomBag != null)
            return ApiResponse.UnprocessableEntity(
                $"Bao #{removableBottomBag.BagNo} là bao dư: bỏ bao này vẫn đủ {order.ComputedPaddyKg:N3} kg cần xay. Vui lòng tự chọn lại nguồn phù hợp.");

        var now = DateTimeHelper.VietnamNow();
        await using var tx = await _millingOrderRepository.BeginTransactionAsync();
        try
        {
            foreach (var oldInput in order.MillingOrderInputs.Where(x => !x.IsDeleted))
            {
                var oldLot = await _paddyLotRepository.GetByIdAsync(oldInput.PaddyLotId);
                if (oldLot != null)
                {
                    var oldLocationId = oldInput.LocationId ?? oldLot.LocationId;
                    var oldInventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(oldLot.ProductVariantId, oldLot.WarehouseId, oldLocationId, oldLot.Id);
                    if (oldInventory != null)
                    {
                        oldInventory.QuantityReserved = Math.Max(0, oldInventory.QuantityReserved - oldInput.ReservedWeightKg.GetValueOrDefault());
                        await _inventoryRepository.UpdateAsync(oldInventory);
                    }
                }
                await _inputRepository.SoftDeleteAsync(oldInput.Id);
            }
            var oldAllocations = await _bagAllocationRepository.FindByConditionAsync(x =>
                x.ReferenceType == PaddyLotBagAllocationReferenceTypes.MillingOrder && x.ReferenceId == order.Id &&
                x.Status == PaddyLotBagAllocationStatuses.Active && !x.IsDeleted, true);
            foreach (var allocation in oldAllocations)
            {
                allocation.Status = PaddyLotBagAllocationStatuses.Released;
                allocation.UpdatedBy = userId;
                allocation.LastModifiedDate = now;
                await _bagAllocationRepository.UpdateAsync(allocation);
            }
            await _bagAllocationRepository.SaveChangesAsync();

            foreach (var group in selectedBags.GroupBy(x => new { x.LotId, LocationId = x.LocationId!.Value }))
            {
                var lot = group.First().Lot;
                var weight = group.Sum(x => x.WeightKg);
                var inventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(lot.ProductVariantId, order.WarehouseId, group.Key.LocationId, lot.Id);
                if (inventory == null || inventory.QuantityOnHand - inventory.QuantityReserved < weight - 0.0005m)
                    throw new InvalidOperationException($"Tồn kho lô {lot.LotCode} không đủ để giữ toàn bộ các bao đã chọn.");
                inventory.QuantityReserved += weight;
                inventory.LastModifiedDate = now;
                await _inventoryRepository.UpdateAsync(inventory);

                var input = new MillingOrderInput
                {
                    MillingOrderId = order.Id,
                    PaddyLotId = lot.Id,
                    LocationId = group.Key.LocationId,
                    ReservedWeightKg = weight,
                    ConsumedWeightKg = 0,
                    Note = $"Giữ nguyên bao: {string.Join(", ", group.Select(x => $"#{x.BagNo}"))}",
                    CreatedBy = userId,
                    CreatedDate = now
                };
                await _inputRepository.CreateAsync(input);
                await _inputRepository.SaveChangesAsync();
                foreach (var bag in group)
                {
                    await _bagAllocationRepository.CreateAsync(new PaddyLotBagAllocation
                    {
                        BagId = bag.Id,
                        ReferenceType = PaddyLotBagAllocationReferenceTypes.MillingOrder,
                        ReferenceId = order.Id,
                        ReferenceItemId = input.Id,
                        AllocatedWeightKg = bag.WeightKg,
                        ConsumedWeightKg = 0,
                        Status = PaddyLotBagAllocationStatuses.Active,
                        BagWeightSnapshotKg = bag.WeightKg,
                        StackOrderSnapshot = bag.StackOrder,
                        CreatedBy = userId,
                        CreatedDate = now
                    });
                }
            }
            await _bagAllocationRepository.SaveChangesAsync();
            if (!isInProgressReallocation)
            {
                var reservedStatus = await _statusRepository.FirstOrDefaultAsync(x => x.Code == LookupCodes.MillingOrderStatus.Reserved && !x.IsDeleted)
                    ?? throw new InvalidOperationException("Không tìm thấy trạng thái RESERVED.");
                order.StatusId = reservedStatus.Id;
            }
            order.UpdatedBy = userId;
            order.LastModifiedDate = now;
            await _millingOrderRepository.UpdateAsync(order);
            await _millingOrderRepository.SaveChangesAsync();
            await tx.CommitAsync();
            var total = selectedBags.Sum(x => x.WeightKg);
            return ApiResponse.Success(new { order.Id, ActualSelectedPaddyKg = total, PlannedPaddyKg = order.ComputedPaddyKg, DifferenceKg = total - order.ComputedPaddyKg },
                isReallocation ? "Đã phân bổ lại đúng các bao lúa vật lý." : "Đã giữ đúng các bao lúa vật lý.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync();
            return ApiResponse.Conflict(
                "Tồn kho hoặc vị trí bao vừa được thay đổi bởi thao tác khác. Vui lòng tải lại và chọn nguồn lúa mới.",
                "MILLING_SOURCE_CHANGED");
        }
        catch (DbUpdateException ex) when (IsActiveBagAllocationConflict(ex))
        {
            await tx.RollbackAsync();
            // Hai request có thể cùng vượt qua bước kiểm tra trước khi một request commit.
            // Unique ActiveBagId là chốt cuối; chuyển lỗi DB thành xung đột nghiệp vụ rõ ràng.
            return ApiResponse.Conflict(
                "Một hoặc nhiều bao vừa được chứng từ khác giữ. Vui lòng tải lại và chọn nguồn lúa mới.",
                "PADDY_BAG_ALREADY_HELD");
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

    private static bool IsActiveBagAllocationConflict(DbUpdateException exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) &&
                (message.Contains("IX_PaddyLotBagAllocation_ActiveBagId", StringComparison.OrdinalIgnoreCase) ||
                 message.Contains("PaddyLotBagAllocation.IX_PaddyLotBagAllocation_ActiveBagId", StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }

    public async Task<ApiResponse> SuggestSourcesAsync(int id)
    {
        var order = await _millingOrderRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted, false, x => x.Status, x => x.MillingOrderInputs)
            .FirstOrDefaultAsync();
        if (order == null) return ApiResponse.Error("Không tìm thấy lệnh xay.", 404);
        if (order.Status?.Code is not (LookupCodes.MillingOrderStatus.Draft or LookupCodes.MillingOrderStatus.Reserved))
            return ApiResponse.UnprocessableEntity("Chỉ có thể gợi ý nguồn lúa cho lệnh Nháp/Đã giữ/Đang xay.");

        // Ưu tiên giống đã đóng dấu trên lệnh. Các nhánh dưới chỉ dùng để tương
        // thích dữ liệu cũ được tạo trước khi MillingOrder có RiceVarietyId.
        int? riceVarietyId = order.RiceVarietyId;
        var currentLotIds = order.MillingOrderInputs.Where(x => !x.IsDeleted).Select(x => x.PaddyLotId).Distinct().ToList();
        if (currentLotIds.Count > 0)
        {
            riceVarietyId = await _paddyLotRepository.FindByCondition(x => currentLotIds.Contains(x.Id) && !x.IsDeleted)
                .Where(x => x.RiceVarietyId.HasValue)
                .Select(x => x.RiceVarietyId)
                .FirstOrDefaultAsync();
        }
        if (!riceVarietyId.HasValue && order.SalesOrderId.HasValue)
        {
            riceVarietyId = await _salesOrderRepository.FindByCondition(x => x.Id == order.SalesOrderId.Value && !x.IsDeleted)
                .SelectMany(x => x.SalesOrderItems.Where(i => !i.IsDeleted))
                .Where(x => x.ProductVariant.RiceVarietyId.HasValue)
                .Select(x => x.ProductVariant.RiceVarietyId)
                .FirstOrDefaultAsync();
        }
        if (!riceVarietyId.HasValue)
            return ApiResponse.UnprocessableEntity(
                "Chưa xác định được giống lúa của lệnh xay nên hệ thống không tự chọn lẫn giống. Vui lòng chọn một lô đúng giống lần đầu.");

        var result = new MillingSourceSuggestionResultDto { RequiredWeightKg = order.ComputedPaddyKg };
        if (_bagRepository == null)
            return ApiResponse.UnprocessableEntity("Kho chưa có dữ liệu bao vật lý để tự chọn nguyên bao.");
        var stacks = await _bagRepository
            .FindByCondition(x => x.LocationId.HasValue && x.Location!.WarehouseId == order.WarehouseId &&
                !x.Location.IsOutboundStaging && !x.Location.OutboundLockOrderId.HasValue &&
                x.Status == PaddyLotBagStatuses.Stored && !x.IsDeleted, false)
            .Include(x => x.Location)
            .Include(x => x.Lot).ThenInclude(x => x.Status)
            .Include(x => x.Allocations)
            .OrderBy(x => x.LocationId).ThenByDescending(x => x.StackOrder).ThenByDescending(x => x.Id)
            .ToListAsync();
        var selectable = new List<PaddyLotBag>();
        foreach (var stack in stacks.GroupBy(x => x.LocationId!.Value))
        {
            foreach (var bag in stack)
            {
                var heldByOther = bag.Allocations.Any(x => !x.IsDeleted && x.Status == PaddyLotBagAllocationStatuses.Active &&
                    !(x.ReferenceType == PaddyLotBagAllocationReferenceTypes.MillingOrder && x.ReferenceId == order.Id));
                if (heldByOther || bag.Lot.Status.Code == LotStatusCodeConstants.Quarantine ||
                    bag.Lot.RiceVarietyId != riceVarietyId || !string.Equals(bag.Lot.LotType, "PADDY", StringComparison.OrdinalIgnoreCase))
                    break;
                selectable.Add(bag);
            }
        }
        // Chọn từng bao một từ đỉnh cột. Sau mỗi bao phải kiểm tra ngay lượng cần giữ
        // để không kéo thêm cả prefix của cột khi bao đầu tiên đã đủ.
        var availableColumns = selectable
            .GroupBy(x => x.LocationId!.Value)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(b => b.StackOrder).ThenByDescending(b => b.Id).ToList());
        var columnOffsets = availableColumns.Keys.ToDictionary(x => x, _ => 0);
        var selected = new List<PaddyLotBag>();
        var selectedKg = 0m;
        while (selectedKg + 0.0005m < order.ComputedPaddyKg)
        {
            // Chỉ so sánh các bao hiện đang nằm trên cùng của từng cột. FIFO theo ngày nhập
            // của lô, sau đó ổn định theo vị trí/thứ tự bao.
            var nextBag = availableColumns
                .Where(x => columnOffsets[x.Key] < x.Value.Count)
                .Select(x => x.Value[columnOffsets[x.Key]])
                .OrderBy(x => x.Lot.InboundDate)
                .ThenBy(x => x.LocationId)
                .ThenByDescending(x => x.StackOrder)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();
            if (nextBag == null) break;

            selected.Add(nextBag);
            selectedKg += nextBag.WeightKg;
            columnOffsets[nextBag.LocationId!.Value]++;
        }
        foreach (var group in selected.GroupBy(x => new { x.LotId, LocationId = x.LocationId!.Value }))
        {
            var first = group.First();
            var groupWeight = group.Sum(x => x.WeightKg);
            result.Inputs.Add(new MillingSourceSuggestionDto
            {
                PaddyLotId = group.Key.LotId,
                LotCode = first.Lot.LotCode,
                LocationId = group.Key.LocationId,
                LocationCode = first.Location?.SlotCode,
                SuggestedWeightKg = groupWeight,
                ImmediatelyRetrievableKg = groupWeight,
                BagIds = group.OrderByDescending(x => x.StackOrder).Select(x => x.Id).ToList()
            });
        }
        result.Columns = selected.GroupBy(x => x.LocationId!.Value).Select(group => new MillingSourceSuggestionColumnDto
        {
            LocationId = group.Key,
            LocationCode = group.First().Location?.SlotCode,
            SuggestedWeightKg = group.Sum(x => x.WeightKg),
            BagIds = group.OrderByDescending(x => x.StackOrder).ThenByDescending(x => x.Id).Select(x => x.Id).ToList()
        }).ToList();
        result.SuggestedWeightKg = result.Inputs.Sum(x => x.SuggestedWeightKg);
        result.MissingWeightKg = Math.Max(0, order.ComputedPaddyKg - result.SuggestedWeightKg);
        return ApiResponse.Success(result, result.IsComplete
            ? "Đã tìm thấy nguồn lúa có thể lấy ngay."
            : $"Nguồn có thể lấy ngay còn thiếu {result.MissingWeightKg:N3} kg.");
    }

    /// <summary>Bắt đầu lệnh xay; máy xay và người vận hành được ghi nhận khi hoàn thành.</summary>
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

        order.StatusId   = inProgressStatus.Id;
        order.StartedAt  = DateTimeHelper.VietnamNow();
        order.UpdatedBy         = userId;
        order.LastModifiedDate  = DateTimeHelper.VietnamNow();

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
                foreach (var input in order.MillingOrderInputs.Where(x => !x.IsDeleted))
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
                if (_bagAllocationRepository != null)
                {
                    var bagAllocations = await _bagAllocationRepository.FindByConditionAsync(x =>
                        x.ReferenceType == PaddyLotBagAllocationReferenceTypes.MillingOrder && x.ReferenceId == order.Id &&
                        x.Status == PaddyLotBagAllocationStatuses.Active && !x.IsDeleted, true);
                    foreach (var allocation in bagAllocations)
                    {
                        allocation.Status = PaddyLotBagAllocationStatuses.Released;
                        allocation.UpdatedBy = userId;
                        allocation.LastModifiedDate = now;
                        await _bagAllocationRepository.UpdateAsync(allocation);
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
        var activeInputs = order.MillingOrderInputs.Where(x => !x.IsDeleted).ToList();
        if (activeInputs.Count == 0)
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

        if (order.SalesOrderId.HasValue)
        {
            var salesOrder = await _salesOrderRepository.GetByIdDetailAsync(order.SalesOrderId.Value);
            if (salesOrder == null)
                return ApiResponse.UnprocessableEntity("Không tìm thấy đơn bán gắn với lệnh xay.");

            var requiredRice = salesOrder.SalesOrderItems
                .Where(x => !x.IsDeleted && !x.ProductVariant.IsByproduct)
                .GroupBy(x => x.ProductVariantId)
                .ToDictionary(x => x.Key, x => x.Sum(i => i.QuantityOrdered));
            var actualRice = dto.Outputs
                .Where(x => string.Equals(x.OutputType?.Trim(), "RICE", StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.ProductVariantId)
                .ToDictionary(x => x.Key, x => x.Sum(x => x.OutputWeightKg));

            var wrongVariant = actualRice.Keys.FirstOrDefault(x => !requiredRice.ContainsKey(x));
            if (wrongVariant > 0)
                return ApiResponse.UnprocessableEntity(
                    $"SKU gạo đầu ra ID {wrongVariant} không có trong đơn bán. Vui lòng chọn đúng SKU gạo khách đã đặt.");
            foreach (var required in requiredRice)
            {
                actualRice.TryGetValue(required.Key, out var producedKg);
                if (producedKg + 0.0005m < required.Value)
                    return ApiResponse.UnprocessableEntity(
                        $"SKU gạo ID {required.Key} cần tối thiểu {required.Value:N3} kg theo đơn bán, hiện khai báo {producedKg:N3} kg.");
            }
        }

        // W14-J: Xác định MachineRef và OperatorId khi Complete — ưu tiên DTO,
        // fallback về giá trị hiện có trên lệnh để tương thích dữ liệu cũ.
        var finalMachineRef = !string.IsNullOrWhiteSpace(dto.MachineRef)
            ? dto.MachineRef.Trim()
            : order.MachineRef;
        var finalOperatorId = dto.OperatorId ?? order.OperatorId;

        if (string.IsNullOrWhiteSpace(finalMachineRef))
            return ApiResponse.UnprocessableEntity(
                "Mã máy xay (MachineRef) là bắt buộc để hoàn thành lệnh xay.",
                "MILLING_MACHINE_REF_REQUIRED");
        if (!finalOperatorId.HasValue)
            return ApiResponse.UnprocessableEntity(
                "ID người vận hành (OperatorId) là bắt buộc để hoàn thành lệnh xay.",
                "MILLING_OPERATOR_REQUIRED");
        if (!await IsEligibleMillingOperatorAsync(finalOperatorId.Value))
            return ApiResponse.UnprocessableEntity(
                "Người vận hành phải là tài khoản đang hoạt động có vai trò Nhân viên xay xát.",
                "MILLING_OPERATOR_INVALID");

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

        if (_bagAllocationRepository == null)
            return ApiResponse.UnprocessableEntity("Lệnh xay chưa có dữ liệu giữ bao vật lý.");
        var activeBagAllocations = await _bagAllocationRepository
            .FindByCondition(x => x.ReferenceType == PaddyLotBagAllocationReferenceTypes.MillingOrder &&
                x.ReferenceId == order.Id && x.Status == PaddyLotBagAllocationStatuses.Active && !x.IsDeleted, false)
            .Include(x => x.Bag)
            .ToListAsync();
        if (activeBagAllocations.Count == 0)
            return ApiResponse.UnprocessableEntity("Lệnh xay chưa chọn và giữ bao lúa vật lý.");
        if (activeBagAllocations.Any(x => x.Bag.Status != PaddyLotBagStatuses.Stored ||
            Math.Abs(x.Bag.WeightKg - x.BagWeightSnapshotKg) > 0.0005m))
            return ApiResponse.Conflict("Một hoặc nhiều bao đã thay đổi sau khi giữ. Vui lòng điều chỉnh lại nguồn lúa.");
        var actualPaddyInputKg = activeBagAllocations.Sum(x => x.BagWeightSnapshotKg);
        var totalProducedKg = dto.Outputs.Sum(x => x.OutputWeightKg);
        var lossKg = dto.LossKg ?? 0;
        if (lossKg < 0)
            return ApiResponse.UnprocessableEntity("Khối lượng hao hụt không được âm.");

        var massBalanceTolerance = actualPaddyInputKg * 0.02m;
        if ((totalProducedKg + lossKg) > (actualPaddyInputKg + massBalanceTolerance))
        {
            return ApiResponse.UnprocessableEntity(
                $"Mass balance không hợp lệ: Tổng đầu ra ({totalProducedKg} kg) + Hao hụt ({lossKg} kg) " +
                $"vượt quá lượng lúa nguyên bao thực tế ({actualPaddyInputKg} kg) + 2% dung sai.");
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

            foreach (var input in activeInputs.OrderBy(x => x.CreatedDate).ThenBy(x => x.Id))
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
            var remainingPaddyKg = actualPaddyInputKg;

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
                await ConsumeMillingInputBagsAsync(state.Lot.Id, state.Input.LocationId ?? state.Lot.LocationId!.Value, consumedKg, orderId, state.Input.Id, completedById, now);
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

            foreach (var bagAllocation in activeBagAllocations)
            {
                bagAllocation.ConsumedWeightKg = bagAllocation.AllocatedWeightKg;
                bagAllocation.Status = PaddyLotBagAllocationStatuses.Consumed;
                bagAllocation.UpdatedBy = completedById;
                bagAllocation.LastModifiedDate = now;
                await _bagAllocationRepository.UpdateAsync(bagAllocation);
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

                var actualBagCount = await PackageMillingOutputAsync(newLot, outputDto.OutputWeightKg, outputDto.LocationId.Value, completedById, now);

                var output = new MillingOrderOutput
                {
                    MillingOrderId = order.Id,
                    ProductVariantId = outputDto.ProductVariantId,
                    OutputLotId = newLot.Id,
                    LocationId = outputDto.LocationId,
                    OutputType = outputType,
                    OutputWeightKg = outputDto.OutputWeightKg,
                    BagCount = actualBagCount,
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
            order.ActualPaddyInputKg = actualPaddyInputKg;
            order.ActualYieldRate = actualPaddyInputKg > 0 ? totalActualRice / actualPaddyInputKg : null;
            order.ByproductKg = dto.Outputs
                .Where(x => !string.Equals(x.OutputType?.Trim(), "RICE", StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.OutputWeightKg);
            order.LossKg = lossKg;
            order.TotalCost = totalCostToAllocate;
            // Persist the resolved values; preserving existing values when the Complete request
            // omits them keeps legacy trace data intact.
            order.MachineRef = finalMachineRef;
            order.OperatorId = finalOperatorId;
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
                    PlannedPaddyKg = order.ComputedPaddyKg,
                    ActualPaddyInputKg = actualPaddyInputKg,
                    ActualYieldRate = order.ActualYieldRate,
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

    private async Task<int> PackageMillingOutputAsync(PaddyLot lot, decimal quantityKg, int locationId, int userId, DateTime now)
    {
        // Legacy compatibility: databases/tests without the bag repositories keep the existing kg-only flow.
        if (_bagRepository == null || _bagContentRepository == null) return 0;
        var affectedBagIds = new HashSet<int>();
        var lotWithVariant = await _paddyLotRepository.FindByCondition(x => x.Id == lot.Id && !x.IsDeleted, false)
            .Include(x => x.ProductVariant).FirstOrDefaultAsync();
        var standardKg = lotWithVariant?.ProductVariant.Weight ?? 0;
        if (standardKg <= 0)
            throw new InvalidOperationException(
                $"Biến thể '{lotWithVariant?.ProductVariant.SKU ?? lot.ProductVariantId.ToString()}' chưa cấu hình khối lượng bao chuẩn hoặc giá trị không hợp lệ.");

        var remaining = quantityKg;
        var openBags = await _bagRepository.FindByCondition(x => x.BagKind == "Finished" && !x.IsFull && x.StandardWeightKg == standardKg && x.Status == "Stored" && !x.IsDeleted && x.LocationId == locationId)
            .Include(x => x.Lot)
            .Include(x => x.Contents).ThenInclude(x => x.Lot).ThenInclude(x => x.Status)
            .Where(x => x.Lot.ProductVariantId == lot.ProductVariantId && x.Lot.WarehouseId == lot.WarehouseId)
            .OrderByDescending(x => x.StackOrder).ToListAsync();
        if (openBags.Count > 1)
            throw new InvalidOperationException($"Có nhiều hơn một bao mở cho variant {lot.ProductVariantId} tại vị trí {locationId}.");
        if (openBags.Count == 1 && remaining > 0)
        {
            var open = openBags[0];
            if (open.StackOrder != 0)
                throw new InvalidOperationException($"Bao mở #{open.BagNo} đang bị bao khác chặn phía trên nên không thể bổ sung sản lượng xay.");
            if (open.Contents.Any(x => !x.IsDeleted && x.WeightKg > 0 &&
                    (x.Lot.Status?.Code == LotStatusCodeConstants.Quarantine || x.Lot.Status?.IsSellable == false)))
                throw new InvalidOperationException($"Bao mở #{open.BagNo} có thành phần lô đang cách ly hoặc không được phép sử dụng.");
            var topUp = Math.Min(remaining, standardKg - open.WeightKg);
            if (topUp > 0)
            {
                var beforeWeight = open.WeightKg;
                await _bagContentRepository.CreateAsync(new PaddyLotBagContent { BagId = open.Id, LotId = lot.Id, WeightKg = topUp, CreatedBy = userId, CreatedDate = now });
                affectedBagIds.Add(open.Id);
                open.WeightKg += topUp; open.IsFull = open.WeightKg >= standardKg; open.OpenBagKey = open.IsFull ? null : BuildOpenBagKey(lot.ProductVariantId, lot.WarehouseId, locationId); open.UpdatedBy = userId; open.LastModifiedDate = now;
                await _bagRepository.UpdateAsync(open); remaining -= topUp;
                if (_bagMovementRepository != null && lot.SourceMillingOrderId.HasValue)
                    await _bagMovementRepository.CreateAsync(new PaddyLotBagMovement
                    {
                        BagId = open.Id, MovementType = PaddyLotBagMovementTypes.MillingPack,
                        FromLocationId = locationId, ToLocationId = locationId,
                        WeightKg = topUp, BeforeWeightKg = beforeWeight, AfterWeightKg = open.WeightKg,
                        ReferenceType = PaddyLotBagAllocationReferenceTypes.MillingOrder,
                        ReferenceId = lot.SourceMillingOrderId.Value,
                        Note = $"Bổ sung {topUp:0.###} kg từ lô {lot.LotCode} vào bao mở",
                        CreatedBy = userId, CreatedDate = now
                    });
            }
        }

        var nextBagNo = (await _bagRepository.FindByCondition(x => x.LotId == lot.Id && !x.IsDeleted).MaxAsync(x => (int?)x.BagNo) ?? 0) + 1;
        var nextStack = (await _bagRepository.FindByCondition(x => x.LocationId == locationId && x.Status == "Stored" && !x.IsDeleted &&
            (x.IsFull || x.BagKind != PaddyLotBagKinds.Finished)).MaxAsync(x => (int?)x.StackOrder) ?? 0) + 1;
        while (remaining > 0.0005m)
        {
            var weight = Math.Min(remaining, standardKg);
            var bag = new PaddyLotBag
            {
                LotId = lot.Id, BagNo = nextBagNo++, WeightKg = weight, LocationId = locationId,
                StackOrder = weight < standardKg ? 0 : nextStack++, StandardWeightKg = standardKg, IsFull = weight >= standardKg,
                BagKind = "Finished", Status = "Stored", QrCode = $"PLB-{Guid.NewGuid():N}".ToUpperInvariant(),
                OpenBagKey = weight < standardKg ? BuildOpenBagKey(lot.ProductVariantId, lot.WarehouseId, locationId) : null,
                CreatedBy = userId, CreatedDate = now
            };
            await _bagRepository.CreateAsync(bag);
            await _bagRepository.SaveChangesAsync();
            affectedBagIds.Add(bag.Id);
            await _bagContentRepository.CreateAsync(new PaddyLotBagContent { BagId = bag.Id, LotId = lot.Id, WeightKg = weight, CreatedBy = userId, CreatedDate = now });
            if (_bagMovementRepository != null && lot.SourceMillingOrderId.HasValue)
                await _bagMovementRepository.CreateAsync(new PaddyLotBagMovement
                {
                    BagId = bag.Id, MovementType = PaddyLotBagMovementTypes.MillingPack,
                    FromLocationId = null, ToLocationId = locationId,
                    WeightKg = weight, BeforeWeightKg = 0, AfterWeightKg = weight,
                    ReferenceType = PaddyLotBagAllocationReferenceTypes.MillingOrder,
                    ReferenceId = lot.SourceMillingOrderId.Value,
                    Note = $"Đóng bao thành phẩm từ lô {lot.LotCode}",
                    CreatedBy = userId, CreatedDate = now
                });
            remaining -= weight;
        }
        await _bagContentRepository.SaveChangesAsync();
        return affectedBagIds.Count;
    }

    private static string BuildOpenBagKey(int variantId, int warehouseId, int locationId)
        => $"{variantId}:{warehouseId}:{locationId}";

    private async Task ConsumeMillingInputBagsAsync(int lotId, int locationId, decimal requestedKg, int orderId, int inputId, int userId, DateTime now)
    {
        if (_bagRepository == null || _bagContentRepository == null) return;
        var targetLot = await _paddyLotRepository.GetByIdAsync(lotId);
        if (targetLot == null) throw new InvalidOperationException($"Không tìm thấy lô {lotId} của bao vật lý.");
        var retrieval = await GetBagRetrievalStateAsync(lotId, locationId, requestedKg);
        if (retrieval.HasPhysicalBags && !retrieval.CanRetrieve)
            throw new InvalidOperationException(BuildBagRetrievalError(targetLot.LotCode, locationId, requestedKg, retrieval));

        var bags = await _bagRepository.FindByCondition(x => x.LocationId == locationId && x.Status == "Stored" && !x.IsDeleted, true)
            .Include(x => x.Lot).Include(x => x.Contents)
            .OrderByDescending(x => x.StackOrder).ToListAsync();
        var available = bags.Sum(x => x.Contents.Where(c => c.LotId == lotId && !c.IsDeleted).Sum(c => c.WeightKg));
        if (available + 0.0005m < requestedKg)
            throw new InvalidOperationException($"Không đủ bao vật lý LIFO của lô {lotId} tại vị trí {locationId}.");
        var remaining = requestedKg;
        foreach (var bag in bags)
        {
            var beforeBagWeight = bag.WeightKg;
            var fromLocationId = bag.LocationId;
            var targetContents = bag.Contents.Where(x => x.LotId == lotId && x.WeightKg > 0 && !x.IsDeleted).OrderBy(x => x.Id).ToList();
            if (targetContents.Count == 0)
                throw new InvalidOperationException(BuildBagRetrievalError(targetLot.LotCode, locationId, requestedKg, retrieval));
            foreach (var content in targetContents)
            {
                var take = Math.Min(remaining, content.WeightKg);
                content.WeightKg -= take; bag.WeightKg -= take; remaining -= take;
                content.UpdatedBy = userId; content.LastModifiedDate = now;
                await _bagContentRepository.UpdateAsync(content);
                if (remaining <= 0.0005m) break;
            }
            bag.WeightKg = Math.Max(0, bag.WeightKg);
            if (bag.WeightKg <= 0.0005m)
            {
                bag.WeightKg = 0;
                bag.Status = "Consumed";
                bag.LocationId = null;
                bag.IsFull = false;
                bag.OpenBagKey = null;
            }
            else if (bag.StandardWeightKg.HasValue)
            {
                bag.IsFull = bag.WeightKg >= bag.StandardWeightKg.Value;
                bag.OpenBagKey = bag.IsFull
                    ? null
                    : BuildOpenBagKey(targetLot.ProductVariantId, targetLot.WarehouseId, locationId);
            }
            else
            {
                bag.IsFull = false;
                bag.OpenBagKey = null;
            }
            RefreshRepresentativeLot(bag);
            bag.UpdatedBy = userId; bag.LastModifiedDate = now;
            await _bagRepository.UpdateAsync(bag);
            if (bag.Status == PaddyLotBagStatuses.Consumed && _bagMovementRepository != null)
            {
                await _bagMovementRepository.CreateAsync(new PaddyLotBagMovement
                {
                    BagId = bag.Id,
                    MovementType = PaddyLotBagMovementTypes.MillingConsume,
                    FromLocationId = fromLocationId,
                    ToLocationId = null,
                    WeightKg = beforeBagWeight,
                    BeforeWeightKg = beforeBagWeight,
                    AfterWeightKg = 0,
                    ReferenceType = PaddyLotBagAllocationReferenceTypes.MillingOrder,
                    ReferenceId = orderId,
                    ReferenceItemId = inputId,
                    CreatedBy = userId,
                    CreatedDate = now
                });
            }
            if (remaining <= 0.0005m) break;
        }
        await _bagContentRepository.SaveChangesAsync();
    }

    private static void RefreshRepresentativeLot(PaddyLotBag bag)
    {
        if (bag.WeightKg <= 0) return;
        var active = bag.Contents.Where(x => !x.IsDeleted && x.WeightKg > 0.0005m).ToList();
        if (active.Count == 0)
            throw new InvalidOperationException($"Bao #{bag.BagNo} còn khối lượng nhưng không còn thành phần lô.");
        if (active.Any(x => x.LotId == bag.LotId)) return;
        bag.LotId = active.OrderByDescending(x => x.WeightKg).ThenBy(x => x.Id).First().LotId;
    }

    private sealed record BagRetrievalState(
        bool HasPhysicalBags,
        bool CanRetrieve,
        decimal RetrievableKg,
        decimal MissingKg,
        int BlockingBagCount,
        int? FirstBlockingBagNo,
        string? FirstBlockingLotCode);

    private async Task<BagRetrievalState> GetBagRetrievalStateAsync(int lotId, int locationId, decimal requestedKg)
    {
        if (_bagRepository == null || _bagContentRepository == null)
            return new BagRetrievalState(false, true, requestedKg, 0, 0, null, null);

        var bags = await _bagRepository
            .FindByCondition(x => x.LocationId == locationId && x.Status == "Stored" && !x.IsDeleted, true)
            .Include(x => x.Lot)
            .Include(x => x.Contents)
            .OrderByDescending(x => x.StackOrder)
            .ToListAsync();

        if (bags.Count == 0)
            return new BagRetrievalState(false, true, requestedKg, 0, 0, null, null);

        decimal retrievableKg = 0;
        PaddyLotBag? firstBlocker = null;
        foreach (var bag in bags)
        {
            var targetWeight = bag.Contents
                .Where(x => x.LotId == lotId && !x.IsDeleted && x.WeightKg > 0)
                .Sum(x => x.WeightKg);
            if (targetWeight <= 0)
            {
                firstBlocker = bag;
                break;
            }

            retrievableKg += targetWeight;
            if (retrievableKg + 0.0005m >= requestedKg) break;
        }

        var canRetrieve = retrievableKg + 0.0005m >= requestedKg;
        var blockingCount = 0;
        if (!canRetrieve && firstBlocker != null)
        {
            var reachedBlocker = false;
            foreach (var bag in bags)
            {
                if (bag.Id == firstBlocker.Id) reachedBlocker = true;
                if (!reachedBlocker) continue;
                var containsTarget = bag.Contents.Any(x => x.LotId == lotId && !x.IsDeleted && x.WeightKg > 0);
                if (containsTarget) break;
                blockingCount++;
            }
        }
        return new BagRetrievalState(
            true,
            canRetrieve,
            retrievableKg,
            Math.Max(0, requestedKg - retrievableKg),
            blockingCount,
            firstBlocker?.BagNo,
            firstBlocker?.Lot?.LotCode);
    }

    private static string BuildBagRetrievalError(
        string lotCode,
        int locationId,
        decimal requestedKg,
        BagRetrievalState state)
    {
        var blocker = state.FirstBlockingBagNo.HasValue
            ? $" Bao cản đầu tiên là bao #{state.FirstBlockingBagNo} của lô {state.FirstBlockingLotCode ?? "khác"}."
            : string.Empty;
        return $"Chưa thể lấy đủ {requestedKg:N3} kg từ lô {lotCode} tại vị trí #{locationId}. " +
               $"Có thể lấy ngay {state.RetrievableKg:N3} kg, còn thiếu {state.MissingKg:N3} kg; " +
               $"có {state.BlockingBagCount} bao đang cản phía trên.{blocker} " +
               "Vui lòng chọn thêm lô/vị trí khác hoặc thực hiện đảo các bao cản trước.";
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
        RiceVarietyId = x.RiceVarietyId,
        RiceVarietyName = x.RiceVariety?.Name,
        Reason = x.Reason,
        SalesOrderId = x.SalesOrderId,
        YieldRateUsed = x.YieldRateUsed,
        TotalRiceOutputKg = x.TotalRiceOutputKg,
        ComputedPaddyKg = x.ComputedPaddyKg,
        ActualPaddyInputKg = x.ActualPaddyInputKg,
        ActualYieldRate = x.ActualYieldRate,
        ByproductKg = x.ByproductKg,
        LossKg = x.LossKg,
        MachineRef = x.MachineRef,
        OperatorId = x.OperatorId,
        StartedAt = x.StartedAt,
        CompletedAt = x.CompletedAt,
        TotalCost = x.TotalCost,
        MillingCost = x.Status?.Code == LookupCodes.MillingOrderStatus.Completed ? null : x.TotalCost,
        IncidentalCost = null,
        Inputs = x.MillingOrderInputs.Where(i => !i.IsDeleted).Select(i => new MillingOrderInputDetailDto
        {
            Id = i.Id,
            PaddyLotId = i.PaddyLotId,
            LotCode = i.PaddyLot?.LotCode,
            LocationCode = i.Location?.SlotCode,
            LocationId = i.LocationId,
            ConsumedWeightKg = i.ConsumedWeightKg,
            ReservedWeightKg = i.ReservedWeightKg,
            Note = i.Note
        }).ToList(),
        Outputs = x.MillingOrderOutputs.Where(o => !o.IsDeleted).Select(o => new MillingOrderOutputDetailDto
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
