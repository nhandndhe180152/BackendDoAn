using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.QualityInspections;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ kiểm tra chất lượng lô lúa/gạo (QualityInspection).
/// </summary>
public class QualityInspectionService : IQualityInspectionService
{
    private readonly IQualityInspectionRepository _repo;
    private readonly IPaddyLotRepository _paddyLotRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryTransactionRepository _inventoryTransactionRepository;
    private readonly IRepositoryBase<LotStatus, int> _lotStatusRepository;

    public QualityInspectionService(
        IQualityInspectionRepository repo,
        IPaddyLotRepository paddyLotRepository,
        IInventoryRepository inventoryRepository,
        IInventoryTransactionRepository inventoryTransactionRepository,
        IRepositoryBase<LotStatus, int> lotStatusRepository)
    {
        _repo = repo;
        _paddyLotRepository = paddyLotRepository;
        _inventoryRepository = inventoryRepository;
        _inventoryTransactionRepository = inventoryTransactionRepository;
        _lotStatusRepository = lotStatusRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateQualityInspectionDto obj)
    {
        var lot = await _paddyLotRepository.GetByIdAsync(obj.PaddyLotId);
        if (lot == null || lot.IsDeleted)
            return ApiResponse.NotFound(message: "Không tìm thấy lô lúa/gạo.");

        var now = DateTimeHelper.VietnamNow();
        var entity = new QualityInspection
        {
            PaddyLotId = obj.PaddyLotId,
            InspectorId = obj.InspectorId,
            InspectedAt = obj.InspectedAt,
            MoisturePercent = obj.MoisturePercent,
            ImpurityPercent = obj.ImpurityPercent,
            MoldLevel = obj.MoldLevel?.Trim(),
            PestLevel = obj.PestLevel?.Trim(),
            PackagingStatus = obj.PackagingStatus?.Trim(),
            PassedInspection = obj.PassedInspection,
            Handling = obj.Handling?.Trim(),
            Note = obj.Note?.Trim(),
            CreatedBy = obj.CreatedBy,
            CreatedDate = now,
            AffectedWeightKg = obj.AffectedWeightKg
        };

        bool isSplit = false;
        PaddyLot? childLot = null;

        // Bọc trong transaction: tạo phiếu kiểm tra và cập nhật QualityStatus cùng lúc (#12)
        await using var tx = await _repo.BeginTransactionAsync();
        try
        {
            // Nếu đánh giá không đạt (PassedInspection == false) và số lượng bị ảnh hưởng hợp lệ (0 < AffectedWeightKg < RemainingWeightKg)
            if (!obj.PassedInspection && obj.AffectedWeightKg.HasValue && obj.AffectedWeightKg.Value > 0 && obj.AffectedWeightKg.Value < lot.RemainingWeightKg)
            {
                isSplit = true;

                // 1. Tạo LotCode mới: nối đuôi -Q1, -Q2,...
                string childLotCode = $"{lot.LotCode}-Q1";
                int quarantineIndex = 1;
                while (await _paddyLotRepository.AnyAsync(x => x.LotCode == childLotCode && x.OrganizationId == lot.OrganizationId))
                {
                    quarantineIndex++;
                    childLotCode = $"{lot.LotCode}-Q{quarantineIndex}";
                }

                // 2. Tìm trạng thái QUARANTINE cho lô mới
                var quarantineStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == "QUARANTINE" && !x.IsDeleted);
                if (quarantineStatus == null)
                    throw new InvalidOperationException("Không tìm thấy trạng thái LotStatus 'QUARANTINE' trong hệ thống.");

                // 3. Khởi tạo lô con (childLot)
                childLot = new PaddyLot
                {
                    OrganizationId = lot.OrganizationId,
                    LotCode = childLotCode,
                    LotType = lot.LotType,
                    ProductVariantId = lot.ProductVariantId,
                    RiceVarietyId = lot.RiceVarietyId,
                    StatusId = quarantineStatus.Id,
                    WarehouseId = lot.WarehouseId,
                    LocationId = lot.LocationId,
                    InboundDate = lot.InboundDate,
                    InitialWeightKg = obj.AffectedWeightKg.Value,
                    RemainingWeightKg = obj.AffectedWeightKg.Value,
                    CostPricePerKg = lot.CostPricePerKg,
                    QualityStatus = "FAILED",
                    QrCode = "PL-" + Guid.NewGuid().ToString("N").ToUpper(),
                    ParentLotId = lot.Id,
                    CreatedBy = obj.CreatedBy,
                    CreatedDate = now
                };

                await _paddyLotRepository.CreateAsync(childLot);
                await _paddyLotRepository.SaveChangesAsync();

                // 4. Trừ số lượng ở lô gốc
                lot.RemainingWeightKg -= obj.AffectedWeightKg.Value;
                lot.InitialWeightKg -= obj.AffectedWeightKg.Value;
                lot.QualityStatus = "PASSED"; // Lô gốc phần còn lại là Đạt chất lượng

                // Nếu lô gốc đã được kiểm định đạt, chuyển trạng thái của nó sang IN_STOCK nếu đang là PENDING_INBOUND
                var inStockStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == "IN_STOCK" && !x.IsDeleted);
                if (inStockStatus != null && lot.StatusId != inStockStatus.Id)
                {
                    lot.StatusId = inStockStatus.Id;
                }

                lot.LastModifiedDate = now;
                lot.UpdatedBy = obj.CreatedBy;
                await _paddyLotRepository.UpdateAsync(lot);
                await _paddyLotRepository.SaveChangesAsync();

                // 5. Gắn phiếu kiểm định vào lô con
                entity.PaddyLotId = childLot.Id;
                entity.PassedInspection = false; // Phiếu kiểm định cho lô con này là Không đạt
            }
            else
            {
                // Luồng All-or-nothing cũ
                lot.QualityStatus = obj.PassedInspection ? "PASSED" : "FAILED";

                // Nếu PassedInspection = true, tự động chuyển status sang IN_STOCK
                if (obj.PassedInspection)
                {
                    var inStockStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == "IN_STOCK" && !x.IsDeleted);
                    if (inStockStatus != null)
                    {
                        lot.StatusId = inStockStatus.Id;
                    }
                }
                else
                {
                    var quarantineStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == "QUARANTINE" && !x.IsDeleted);
                    if (quarantineStatus != null)
                    {
                        lot.StatusId = quarantineStatus.Id;
                    }
                }

                lot.LastModifiedDate = now;
                lot.UpdatedBy = obj.CreatedBy;
                await _paddyLotRepository.UpdateAsync(lot);
                await _paddyLotRepository.SaveChangesAsync();
            }

            // Tạo phiếu kiểm định
            await _repo.CreateAsync(entity);
            await _repo.SaveChangesAsync();

            // 6. Xử lý Tồn kho (Inventory) và Giao dịch tồn kho (InventoryTransaction) nếu xảy ra Tách lô
            if (isSplit && childLot != null)
            {
                var parentInventories = await _inventoryRepository
                    .FindByCondition(x => x.PaddyLotId == lot.Id && !x.IsDeleted)
                    .ToListAsync();

                decimal remainingToDeduct = obj.AffectedWeightKg!.Value;

                foreach (var parentInv in parentInventories.OrderByDescending(x => x.QuantityOnHand))
                {
                    if (remainingToDeduct <= 0) break;

                    decimal deductQty = Math.Min(parentInv.QuantityOnHand, remainingToDeduct);
                    if (deductQty <= 0) continue;

                    parentInv.QuantityOnHand -= deductQty;
                    parentInv.LastModifiedDate = now;
                    parentInv.UpdatedBy = obj.CreatedBy;
                    await _inventoryRepository.UpdateAsync(parentInv);

                    // Tạo tồn kho cho lô con ở cùng vị trí
                    var childInv = new Inventory
                    {
                        WarehouseId = parentInv.WarehouseId,
                        LocationId = parentInv.LocationId,
                        ProductVariantId = parentInv.ProductVariantId,
                        PaddyLotId = childLot.Id,
                        CostPrice = parentInv.CostPrice,
                        QuantityOnHand = deductQty,
                        QuantityReserved = 0,
                        CreatedDate = now,
                        CreatedBy = obj.CreatedBy
                    };
                    await _inventoryRepository.CreateAsync(childInv);
                    await _inventoryRepository.SaveChangesAsync();

                    remainingToDeduct -= deductQty;

                    // Ghi nhận giao dịch điều chỉnh tồn kho cho lô gốc (XUẤT điều chỉnh)
                    var txOut = new InventoryTransaction
                    {
                        InventoryId = parentInv.Id,
                        WarehouseId = parentInv.WarehouseId,
                        LocationId = parentInv.LocationId,
                        ProductVariantId = parentInv.ProductVariantId,
                        PaddyLotId = lot.Id,
                        TransactionType = InventoryTransactionTypeConstants.Export,
                        ReferenceType = "QUALITY_INSPECTION_SPLIT",
                        ReferenceId = entity.Id,
                        Quantity = deductQty,
                        BeforeQuantity = parentInv.QuantityOnHand + deductQty,
                        AfterQuantity = parentInv.QuantityOnHand,
                        Note = $"Trừ tồn do tách lô cách ly {childLot.LotCode}",
                        CreatedBy = obj.CreatedBy,
                        CreatedDate = now
                    };
                    await _inventoryTransactionRepository.CreateAsync(txOut);

                    // Ghi nhận giao dịch điều chỉnh tồn kho cho lô con (NHẬP điều chỉnh)
                    var txIn = new InventoryTransaction
                    {
                        InventoryId = childInv.Id,
                        WarehouseId = childInv.WarehouseId,
                        LocationId = childInv.LocationId,
                        ProductVariantId = childInv.ProductVariantId,
                        PaddyLotId = childLot.Id,
                        TransactionType = InventoryTransactionTypeConstants.Import,
                        ReferenceType = "QUALITY_INSPECTION_SPLIT",
                        ReferenceId = entity.Id,
                        Quantity = deductQty,
                        BeforeQuantity = 0,
                        AfterQuantity = deductQty,
                        Note = $"Nhập tồn do tách lô cách ly từ lô gốc {lot.LotCode}",
                        CreatedBy = obj.CreatedBy,
                        CreatedDate = now
                    };
                    await _inventoryTransactionRepository.CreateAsync(txIn);
                }

                await _inventoryTransactionRepository.SaveChangesAsync();
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return ApiResponse.Created(entity.Id, "Tạo phiếu kiểm tra chất lượng thành công.");
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateQualityInspectionDto> objs)
        => Task.FromResult(ApiResponse.Error(message: "CreateList chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> GetAllAsync()
    {
        var entities = await _repo
            .FindByCondition(x => !x.IsDeleted, false, x => x.PaddyLot)
            .OrderByDescending(x => x.InspectedAt)
            .ToListAsync();

        return ApiResponse.Success(entities.Select(ToDto).ToList());
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var entity = await _repo
            .FindByCondition(x => x.Id == id && !x.IsDeleted, false, x => x.PaddyLot)
            .FirstOrDefaultAsync();

        if (entity == null) return ApiResponse.NotFound();
        return ApiResponse.Success(ToDto(entity));
    }

    public async Task<ApiResponse> GetByLotAsync(int paddyLotId)
    {
        var entities = await _repo
            .FindByCondition(x => x.PaddyLotId == paddyLotId && !x.IsDeleted, false, x => x.PaddyLot)
            .OrderByDescending(x => x.InspectedAt)
            .ToListAsync();

        return ApiResponse.Success(entities.Select(ToDto).ToList());
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _repo.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query)
        => Task.FromResult(ApiResponse.Error(message: "GetPaged (SearchQuery) chưa được hỗ trợ.", status: 501));
    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
        => Task.FromResult(ApiResponse.Error(message: "GetPaged (AdvancedSearchQuery) chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> UpdateAsync(UpdateQualityInspectionDto obj)
    {
        var entity = await _repo.GetByIdAsync(obj.Id);
        if (entity == null || entity.IsDeleted) return ApiResponse.NotFound();

        entity.PaddyLotId = obj.PaddyLotId;
        entity.InspectorId = obj.InspectorId;
        entity.InspectedAt = obj.InspectedAt;
        entity.MoisturePercent = obj.MoisturePercent;
        entity.ImpurityPercent = obj.ImpurityPercent;
        entity.MoldLevel = obj.MoldLevel?.Trim();
        entity.PestLevel = obj.PestLevel?.Trim();
        entity.PackagingStatus = obj.PackagingStatus?.Trim();
        entity.PassedInspection = obj.PassedInspection;
        entity.Handling = obj.Handling?.Trim();
        entity.Note = obj.Note?.Trim();
        entity.UpdatedBy = obj.UpdatedBy;
        entity.LastModifiedDate = DateTimeHelper.VietnamNow();

        await _repo.UpdateAsync(entity);
        await _repo.SaveChangesAsync();

        // Đồng bộ QualityStatus về lô lúa/gạo (#14: giữ nhất quán khi phiếu bị cập nhật)
        var lot = await _paddyLotRepository.GetByIdAsync(obj.PaddyLotId);
        if (lot != null && !lot.IsDeleted)
        {
            lot.QualityStatus = obj.PassedInspection ? "PASSED" : "FAILED";
            lot.LastModifiedDate = entity.LastModifiedDate;
            await _paddyLotRepository.UpdateAsync(lot);
            await _paddyLotRepository.SaveChangesAsync();
        }

        return ApiResponse.Success(entity.Id, "Cập nhật phiếu kiểm tra thành công.");
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateQualityInspectionDto> objs)
        => Task.FromResult(ApiResponse.Error(message: "UpdateList chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _repo.SoftDeleteAsync(id);
        if (!isDeleted) return ApiResponse.BadRequest();
        await _repo.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _repo.SoftDeleteListAsync(objs);
        if (!isDeleted) return ApiResponse.BadRequest();
        await _repo.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    private static QualityInspectionDetailDto ToDto(QualityInspection x) => new()
    {
        Id = x.Id,
        PaddyLotId = x.PaddyLotId,
        LotCode = x.PaddyLot?.LotCode,
        InspectorId = x.InspectorId,
        InspectedAt = x.InspectedAt,
        MoisturePercent = x.MoisturePercent,
        ImpurityPercent = x.ImpurityPercent,
        MoldLevel = x.MoldLevel,
        PestLevel = x.PestLevel,
        PackagingStatus = x.PackagingStatus,
        PassedInspection = x.PassedInspection,
        Handling = x.Handling,
        Note = x.Note,
        AffectedWeightKg = x.AffectedWeightKg,
        CreatedDate = x.CreatedDate,
        LastModifiedDate = x.LastModifiedDate
    };
}
