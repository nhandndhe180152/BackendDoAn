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

    public MillingOrderService(
        IMillingOrderRepository millingOrderRepository,
        IPaddyLotRepository paddyLotRepository,
        IRepositoryBase<MillingOrderInput, int> inputRepository,
        IRepositoryBase<MillingOrderOutput, int> outputRepository,
        IRepositoryBase<MillingOrderStatus, int> statusRepository,
        IRepositoryBase<LotStatus, int> lotStatusRepository,
        IInventoryRepository inventoryRepository,
        IInventoryTransactionRepository inventoryTransactionRepository,
        ILocationRepository locationRepository)
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
    }

    public async Task<ApiResponse> CreateAsync(CreateMillingOrderDto obj)
    {
        var now = DateTimeHelper.VietnamNow();
        var datePart = now.ToString("yyyyMMdd");
        var baseCode = $"MO-{datePart}";
        // #10: Đếm tất cả (kể cả IsDeleted) để tránh trùng suffix. Unique index là safety net.
        var count = await _millingOrderRepository
            .FindByCondition(x => x.MillingCode.StartsWith(baseCode))
            .CountAsync();
        var millingCode = $"{baseCode}-{(count + 1):D4}";

        // Lấy status DRAFT hoặc đầu tiên
        var draftStatus = await _statusRepository.FirstOrDefaultAsync(x => x.Name == "Draft" && !x.IsDeleted)
            ?? await _statusRepository.FirstOrDefaultAsync(x => !x.IsDeleted)
            ?? throw new InvalidOperationException("Không tìm thấy MillingOrderStatus.");

        var computedPaddyKg = obj.YieldRateUsed > 0 ? obj.TotalRiceOutputKg / obj.YieldRateUsed : 0;

        // Validate output locations belong to order warehouse
        foreach (var outputDto in obj.Outputs)
        {
            if (outputDto.LocationId.HasValue)
            {
                var loc = await _locationRepository.GetByIdAsync(outputDto.LocationId.Value);
                if (loc == null || loc.IsDeleted || loc.WarehouseId != obj.WarehouseId)
                {
                    return ApiResponse.BadRequest(message: $"Vị trí (LocationId={outputDto.LocationId.Value}) không tồn tại hoặc không thuộc kho (WarehouseId={obj.WarehouseId}).");
                }
            }
        }

        // Validate cân bằng khối lượng (mass balance): tổng đầu ra + hao hụt ≤ tổng lúa đầu vào (dung sai 2%)
        var totalInputKg = obj.Inputs.Sum(i => i.ConsumedWeightKg);
        var totalOutputKg = obj.Outputs.Sum(o => o.OutputWeightKg);
        var totalLossKg = obj.LossKg ?? 0;
        var tolerance = totalInputKg * 0.02m; // dung sai 2%
        if (totalInputKg > 0 && (totalOutputKg + totalLossKg) > (totalInputKg + tolerance))
        {
            return ApiResponse.BadRequest(message:
                $"Mass balance không hợp lệ: Tổng đầu ra ({totalOutputKg} kg) + Hao hụt ({totalLossKg} kg) " +
                $"vượt quá Tổng lúa đầu vào ({totalInputKg} kg) ± 2% dung sai.");
        }

        var order = new MillingOrder
        {
            OrganizationId = obj.OrganizationId,
            MillingCode = millingCode,
            StatusId = draftStatus.Id,
            WarehouseId = obj.WarehouseId,
            Reason = obj.Reason?.Trim(),
            SalesOrderId = obj.SalesOrderId,
            YieldRateUsed = obj.YieldRateUsed,
            TotalRiceOutputKg = obj.TotalRiceOutputKg,
            ComputedPaddyKg = computedPaddyKg,
            ByproductKg = obj.ByproductKg,
            LossKg = obj.LossKg,
            MachineRef = obj.MachineRef?.Trim(),
            OperatorId = obj.OperatorId,
            StartedAt = obj.StartedAt,
            CreatedBy = obj.CreatedBy,
            CreatedDate = now
        };

        await _millingOrderRepository.CreateAsync(order);
        await _millingOrderRepository.SaveChangesAsync();

        // Tạo inputs
        foreach (var inputDto in obj.Inputs)
        {
            var input = new MillingOrderInput
            {
                MillingOrderId = order.Id,
                PaddyLotId = inputDto.PaddyLotId,
                LocationId = inputDto.LocationId,
                ConsumedWeightKg = inputDto.ConsumedWeightKg,
                ReservedWeightKg = inputDto.ReservedWeightKg,
                Note = inputDto.Note?.Trim(),
                CreatedBy = obj.CreatedBy,
                CreatedDate = now
            };
            await _inputRepository.CreateAsync(input);
        }

        // Tạo outputs
        foreach (var outputDto in obj.Outputs)
        {
            var output = new MillingOrderOutput
            {
                MillingOrderId = order.Id,
                ProductVariantId = outputDto.ProductVariantId,
                LocationId = outputDto.LocationId,
                OutputType = outputDto.OutputType.Trim().ToUpper(),
                OutputWeightKg = outputDto.OutputWeightKg,
                BagCount = outputDto.BagCount,
                IsByproduct = outputDto.IsByproduct,
                UnitCost = outputDto.UnitCost,
                CreatedBy = obj.CreatedBy,
                CreatedDate = now
            };
            await _outputRepository.CreateAsync(output);
        }

        await _millingOrderRepository.SaveChangesAsync();

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
        var existData = await _millingOrderRepository.GetByIdAsync(obj.Id);
        if (existData == null || existData.IsDeleted) return ApiResponse.NotFound();

        var completedStatus = await _statusRepository.FirstOrDefaultAsync(x => x.Name == "Completed" && !x.IsDeleted);
        if (completedStatus != null && existData.StatusId == completedStatus.Id)
            return ApiResponse.UnprocessableEntity("Lệnh xay đã hoàn thành, không thể chỉnh sửa.");

        existData.OrganizationId = obj.OrganizationId;
        existData.WarehouseId = obj.WarehouseId;
        existData.Reason = obj.Reason?.Trim();
        existData.SalesOrderId = obj.SalesOrderId;
        existData.YieldRateUsed = obj.YieldRateUsed;
        existData.TotalRiceOutputKg = obj.TotalRiceOutputKg;
        existData.ComputedPaddyKg = obj.YieldRateUsed > 0 ? obj.TotalRiceOutputKg / obj.YieldRateUsed : 0;
        existData.ByproductKg = obj.ByproductKg;
        existData.LossKg = obj.LossKg;
        existData.MachineRef = obj.MachineRef?.Trim();
        existData.OperatorId = obj.OperatorId;
        existData.StartedAt = obj.StartedAt;
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

    /// <summary>
    /// Hoàn thành lệnh xay:
    /// 1. Validate tồn lúa đủ cho từng input
    /// 2. Trừ RemainingWeightKg của từng PaddyLot input
    /// 3. Tạo InventoryTransaction EXPORT cho từng lô lúa đầu vào
    /// 4. Sinh PaddyLot mới (RICE/BYPRODUCT) cho từng output
    /// 5. Upsert Inventory + InventoryTransaction IMPORT cho đầu ra
    /// 6. Cập nhật MillingOrder status = Completed
    /// </summary>
    public async Task<ApiResponse> CompleteMillingOrderAsync(int orderId, int completedById)
    {
        var order = await _millingOrderRepository
            .FindByCondition(x => x.Id == orderId && !x.IsDeleted,
                false,
                x => x.MillingOrderInputs,
                x => x.MillingOrderOutputs)
            .FirstOrDefaultAsync();

        if (order == null) return ApiResponse.NotFound();

        var completedStatus = await _statusRepository.FirstOrDefaultAsync(x => x.Name == "Completed" && !x.IsDeleted);
        if (completedStatus != null && order.StatusId == completedStatus.Id)
            return ApiResponse.UnprocessableEntity("Lệnh xay đã hoàn thành trước đó.");

        var now = DateTimeHelper.VietnamNow();

        // Kiểm tra cân bằng khối lượng (mass balance) trước khi chốt — dung sai 2%
        var totalConsumedKg = order.MillingOrderInputs.Sum(i => i.ConsumedWeightKg);
        var totalProducedKg = order.MillingOrderOutputs.Sum(o => o.OutputWeightKg);
        var lossKg = order.LossKg ?? 0;
        var massBalanceTolerance = totalConsumedKg * 0.02m;
        if (totalConsumedKg > 0 && (totalProducedKg + lossKg) > (totalConsumedKg + massBalanceTolerance))
        {
            return ApiResponse.UnprocessableEntity(
                $"Mass balance không hợp lệ: Tổng đầu ra ({totalProducedKg} kg) + Hao hụt ({lossKg} kg) " +
                $"vượt quá tổng lúa đầu vào ({totalConsumedKg} kg) ± 2% dung sai. " +
                "Vui lòng kiểm tra lại số liệu trước khi hoàn thành lệnh xay.");
        }

        await using var tx = await _millingOrderRepository.BeginTransactionAsync();
        try
        {
            // 1. Tính toán giá vật liệu đầu vào (lúa thô) và validate
            decimal totalMaterialCost = 0;
            foreach (var input in order.MillingOrderInputs)
            {
                var lot = await _paddyLotRepository.GetByIdAsync(input.PaddyLotId);
                if (lot == null) return ApiResponse.NotFound(message: $"Không tìm thấy lô lúa Id={input.PaddyLotId}.");

                // Validate: Đảm bảo lượng lúa còn lại trong lô hàng lớn hơn hoặc bằng lượng cần tiêu thụ
                if (lot.RemainingWeightKg < input.ConsumedWeightKg)
                    return ApiResponse.UnprocessableEntity(
                        $"Lô {lot.LotCode}: tồn kho lúa ({lot.RemainingWeightKg} kg) không đủ để xay ({input.ConsumedWeightKg} kg).");

                totalMaterialCost += input.ConsumedWeightKg * lot.CostPricePerKg;

                // TIÊU HAO NGUYÊN LIỆU (PaddyLot):
                // - Trừ trực tiếp khối lượng tiêu hao vào trường RemainingWeightKg của thực thể lô nguyên liệu.
                lot.RemainingWeightKg -= input.ConsumedWeightKg;
                lot.LastModifiedDate = now;
                await _paddyLotRepository.UpdateAsync(lot);

                // TIÊU HAO NGUYÊN LIỆU (Inventory & InventoryTransaction):
                // - Gọi helper ExportLotInventoryAsync để thực hiện trừ QuantityOnHand vật lý trong kho kệ.
                // - Việc này đảm bảo cả hai bảng đều được giảm đồng thời và khớp số liệu.
                await ExportLotInventoryAsync(lot, input.LocationId, input.ConsumedWeightKg, orderId, input.Id, completedById, now);
            }

            await _paddyLotRepository.SaveChangesAsync();

            // Tính toán giá vốn phân bổ đầu ra theo nguyên tắc kế toán:
            // Tổng giá trị cần phân bổ = Tổng giá vốn lúa thô + Chi phí gia công (order.TotalCost)
            decimal totalCostToAllocate = totalMaterialCost + (order.TotalCost ?? 0);

            decimal explicitlyAssignedCost = 0;
            decimal remainingWeightToAllocate = 0;
            foreach (var output in order.MillingOrderOutputs)
            {
                if (output.UnitCost.HasValue)
                {
                    explicitlyAssignedCost += output.OutputWeightKg * output.UnitCost.Value;
                }
                else
                {
                    remainingWeightToAllocate += output.OutputWeightKg;
                }
            }

            decimal remainingCost = Math.Max(0, totalCostToAllocate - explicitlyAssignedCost);
            decimal defaultUnitCost = remainingWeightToAllocate > 0 ? remainingCost / remainingWeightToAllocate : 0;

            // 4 & 5 — Sinh lô mới và nhập kho cho từng output
            var defaultLotStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => !x.IsDeleted)
                ?? throw new InvalidOperationException("Không tìm thấy LotStatus.");

            var datePart = now.ToString("yyyyMMdd");

            foreach (var output in order.MillingOrderOutputs)
            {
                var lotType = output.IsByproduct ? "BYPRODUCT" : "RICE";
                var baseCode = $"LOT-{lotType}-{datePart}";
                var count = await _paddyLotRepository
                    .FindByCondition(x => x.LotCode.StartsWith(baseCode))
                    .CountAsync();
                var lotCode = $"{baseCode}-{(count + 1):D4}";

                var unitCost = output.UnitCost ?? defaultUnitCost;

                // 4. SINH LÔ GẠO THÀNH PHẨM MỚI (PaddyLot):
                // - Đặt RemainingWeightKg (khối lượng còn lại) bằng khối lượng thành phẩm thu hồi (OutputWeightKg).
                var newLot = new PaddyLot
                {
                    OrganizationId = order.OrganizationId,
                    LotCode = lotCode,
                    LotType = lotType,
                    ProductVariantId = output.ProductVariantId,
                    StatusId = defaultLotStatus.Id,
                    SourceMillingOrderId = orderId,
                    WarehouseId = order.WarehouseId,
                    LocationId = output.LocationId, // Gán vị trí ô kệ nếu có
                    InboundDate = now,
                    InitialWeightKg = output.OutputWeightKg,
                    RemainingWeightKg = output.OutputWeightKg,
                    CostPricePerKg = unitCost,
                    CreatedBy = completedById,
                    CreatedDate = now
                };

                await _paddyLotRepository.CreateAsync(newLot);
                await _paddyLotRepository.SaveChangesAsync();

                // Liên kết OutputLotId vào MillingOrderOutput
                output.OutputLotId = newLot.Id;
                output.LastModifiedDate = now;
                await _outputRepository.UpdateAsync(output);

                // 5. NHẬP KHO THÀNH PHẨM (Inventory & InventoryTransaction):
                // - Gọi helper ImportOutputInventoryAsync để chèn mới hoặc cộng tăng QuantityOnHand vật lý.
                // - Ghi nhận giao dịch nhập kho (IMPORT).
                await ImportOutputInventoryAsync(newLot, output.OutputWeightKg, orderId, output.Id, completedById, now);
            }

            // 6 — Cập nhật status lệnh xay
            if (completedStatus != null)
            {
                order.StatusId = completedStatus.Id;
            }
            order.CompletedAt = now;
            order.UpdatedBy = completedById;
            order.LastModifiedDate = now;

            await _millingOrderRepository.UpdateAsync(order);
            await _millingOrderRepository.SaveChangesAsync();

            await _millingOrderRepository.EndTransactionAsync();

            return ApiResponse.Success(new { OrderId = orderId }, "Hoàn thành lệnh xay. Đã sinh lô gạo/phụ phẩm và cập nhật tồn kho.");
        }
        catch (InvalidOperationException ex)
        {
            await _millingOrderRepository.RollbackTransactionAsync();
            return ApiResponse.UnprocessableEntity(ex.Message);
        }
        catch
        {
            await _millingOrderRepository.RollbackTransactionAsync();
            throw;
        }
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private async Task ExportLotInventoryAsync(PaddyLot lot, int? inputLocationId, decimal qty, int orderId, int inputId, int userId, DateTime now)
    {
        var targetLocationId = inputLocationId ?? lot.LocationId;
        var inventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
            lot.ProductVariantId, lot.WarehouseId, targetLocationId, lot.Id);

        if (inventory == null || inventory.QuantityOnHand < qty)
        {
            throw new InvalidOperationException($"Lô lúa {lot.LotCode} không đủ tồn kho vật lý tại kho {lot.WarehouseId} và ô kệ {targetLocationId} (chỉ còn {inventory?.QuantityOnHand ?? 0} kg) để thực hiện xuất {qty} kg.");
        }

        var before = inventory.QuantityOnHand;
        inventory.QuantityOnHand = Math.Max(0, inventory.QuantityOnHand - qty);
        inventory.LastModifiedDate = now;
        await _inventoryRepository.UpdateAsync(inventory);

        var tx = new InventoryTransaction
        {
            InventoryId = inventory.Id,
            WarehouseId = lot.WarehouseId,
            LocationId = targetLocationId,
            ProductVariantId = lot.ProductVariantId,
            TransactionType = InventoryTransactionTypeConstants.Export,
            ReferenceType = InventoryReferenceTypeConstants.MillingOrder,
            ReferenceId = orderId,
            ReferenceItemId = inputId,
            Quantity = -qty,
            BeforeQuantity = before,
            AfterQuantity = inventory.QuantityOnHand,
            WeightKg = qty,
            Note = $"Xay lúa lô {lot.LotCode}",
            CreatedDate = now,
            CreatedBy = userId
        };

        await _inventoryTransactionRepository.CreateAsync(tx);
        await _inventoryTransactionRepository.SaveChangesAsync();
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
