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
    private readonly IApplicationDbContext _context;

    public QualityInspectionService(
        IQualityInspectionRepository repo,
        IPaddyLotRepository paddyLotRepository,
        IInventoryRepository inventoryRepository,
        IInventoryTransactionRepository inventoryTransactionRepository,
        IRepositoryBase<LotStatus, int> lotStatusRepository,
        IApplicationDbContext context)
    {
        _repo = repo;
        _paddyLotRepository = paddyLotRepository;
        _inventoryRepository = inventoryRepository;
        _inventoryTransactionRepository = inventoryTransactionRepository;
        _lotStatusRepository = lotStatusRepository;
        _context = context;
    }

    public async Task<ApiResponse> CreateAsync(CreateQualityInspectionDto obj)
    {
        var lot = await _paddyLotRepository.GetByIdAsync(obj.PaddyLotId);
        if (lot == null || lot.IsDeleted)
            return ApiResponse.NotFound(message: "Không tìm thấy lô lúa/gạo.");

        // B8. Validate đầu vào AffectedWeightKg
        if (obj.AffectedWeightKg.HasValue)
        {
            if (obj.AffectedWeightKg.Value < 0)
                return ApiResponse.BadRequest(message: "Khối lượng bị ảnh hưởng không được nhỏ hơn 0.");
            if (obj.AffectedWeightKg.Value == 0)
                return ApiResponse.BadRequest(message: "Khối lượng bị ảnh hưởng phải lớn hơn 0.");
            if (obj.AffectedWeightKg.Value >= lot.RemainingWeightKg)
                return ApiResponse.BadRequest(message: $"Khối lượng bị ảnh hưởng ({obj.AffectedWeightKg.Value} kg) phải nhỏ hơn khối lượng còn lại của lô hàng ({lot.RemainingWeightKg} kg).");
        }

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
            var parentInventories = await _inventoryRepository
                .FindByCondition(x => x.PaddyLotId == lot.Id && !x.IsDeleted)
                .ToListAsync();

            // Nếu đánh giá không đạt (PassedInspection == false) và số lượng bị ảnh hưởng hợp lệ (0 < AffectedWeightKg < RemainingWeightKg)
            if (!obj.PassedInspection && obj.AffectedWeightKg.HasValue && obj.AffectedWeightKg.Value > 0 && obj.AffectedWeightKg.Value < lot.RemainingWeightKg)
            {
                // B1. Kiểm tra tổng tồn kho khả dụng trước khi tách
                decimal totalAvailable = parentInventories.Sum(x => Math.Max(0, x.QuantityOnHand - x.QuantityReserved));
                if (totalAvailable < obj.AffectedWeightKg.Value)
                {
                    return ApiResponse.BadRequest(message: $"Không đủ tồn kho khả dụng để thực hiện tách lô cách ly (Tồn kho khả dụng: {totalAvailable} kg, Cần tách: {obj.AffectedWeightKg.Value} kg).");
                }

                isSplit = true;

                // Lưu phiếu kiểm định trước để có Id gán vào ReferenceId của các giao dịch (C1)
                await _repo.CreateAsync(entity);
                await _repo.SaveChangesAsync();

                // 1. Tạo LotCode mới: nối đuôi -Q1, -Q2,... (B3: kiểm tra cả bản ghi đã xoá mềm)
                string childLotCode = $"{lot.LotCode}-Q1";
                int quarantineIndex = 1;
                while (await _context.PaddyLots.AnyAsync(x => x.LotCode == childLotCode && x.OrganizationId == lot.OrganizationId))
                {
                    quarantineIndex++;
                    childLotCode = $"{lot.LotCode}-Q{quarantineIndex}";
                }

                // 2. Tìm trạng thái QUARANTINE cho lô mới
                var quarantineStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == LotStatusCodeConstants.Quarantine && !x.IsDeleted);
                if (quarantineStatus == null)
                    throw new InvalidOperationException("Không tìm thấy trạng thái LotStatus 'QUARANTINE' trong hệ thống.");

                // 3. Khởi tạo lô con (childLot) và copy các trường truy xuất nguồn gốc (B9)
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
                    InitialWeightKg = obj.AffectedWeightKg.Value, // R1: Giữ bằng AffectedWeight để tránh lỗi chia 0 (% còn lại) và vi phạm bất biến Remaining > Initial. Reports sẽ lọc ParentLotId IS NULL để tránh tính trùng.
                    RemainingWeightKg = obj.AffectedWeightKg.Value,
                    CostPricePerKg = lot.CostPricePerKg,
                    QualityStatus = QualityStatusConstants.Failed,
                    QrCode = "PL-" + Guid.NewGuid().ToString("N").ToUpper(),
                    QrImageUrl = lot.QrImageUrl, // C6: Copy QrImageUrl
                    ParentLotId = lot.Id,
                    SourceReceiptId = lot.SourceReceiptId,
                    SourceMillingOrderId = lot.SourceMillingOrderId,
                    CreatedBy = obj.CreatedBy,
                    CreatedDate = now
                };

                await _paddyLotRepository.CreateAsync(childLot);
                await _paddyLotRepository.SaveChangesAsync();

                // 4. Trừ số lượng ở lô gốc (B4: Giữ nguyên InitialWeightKg để lưu vết lịch sử)
                lot.RemainingWeightKg -= obj.AffectedWeightKg.Value;
                lot.QualityStatus = QualityStatusConstants.Passed; // Lô gốc phần còn lại là Đạt chất lượng

                // Nếu lô gốc đã được kiểm định đạt, chuyển trạng thái của nó sang IN_STOCK nếu đang là PENDING_INBOUND
                var inStockStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == LotStatusCodeConstants.InStock && !x.IsDeleted);
                if (inStockStatus != null && lot.StatusId != inStockStatus.Id)
                {
                    lot.StatusId = inStockStatus.Id;
                }

                lot.LastModifiedDate = now;
                lot.UpdatedBy = obj.CreatedBy;
                await _paddyLotRepository.UpdateAsync(lot);
                await _paddyLotRepository.SaveChangesAsync();

                // B6: Giữ phiếu ở lô gốc (entity.PaddyLotId giữ nguyên là lot.Id)
            }
            else
            {
                // Lưu phiếu kiểm định trước để có Id gán vào ReferenceId của các giao dịch (C1)
                await _repo.CreateAsync(entity);
                await _repo.SaveChangesAsync();

                // Luồng All-or-nothing cũ
                lot.QualityStatus = obj.PassedInspection ? QualityStatusConstants.Passed : QualityStatusConstants.Failed;

                // Nếu PassedInspection = true, tự động chuyển status sang IN_STOCK
                if (obj.PassedInspection)
                {
                    var inStockStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == LotStatusCodeConstants.InStock && !x.IsDeleted);
                    if (inStockStatus != null)
                    {
                        lot.StatusId = inStockStatus.Id;
                    }
                }
                else
                {
                    var quarantineStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == LotStatusCodeConstants.Quarantine && !x.IsDeleted);
                    if (quarantineStatus != null)
                    {
                        lot.StatusId = quarantineStatus.Id;
                    }

                    // B10: Ghi nhận giao dịch điều chỉnh khi cả lô fail -> QUARANTINE để nhất quán tồn kho
                    foreach (var inv in parentInventories)
                    {
                        var txQuarantine = new InventoryTransaction
                        {
                            InventoryId = inv.Id,
                            WarehouseId = inv.WarehouseId,
                            LocationId = inv.LocationId,
                            ProductVariantId = inv.ProductVariantId,
                            PaddyLotId = lot.Id,
                            TransactionType = InventoryTransactionTypeConstants.ManualAdjust,
                            ReferenceType = InventoryReferenceTypeConstants.QualityInspectionQuarantine,
                            ReferenceId = entity.Id,
                            Quantity = 0, // C5: Bản ghi đánh dấu, không làm thay đổi số lượng
                            BeforeQuantity = inv.QuantityOnHand,
                            AfterQuantity = inv.QuantityOnHand,
                            Note = "Toàn bộ lô hàng chuyển sang trạng thái CÁCH LY do kiểm định không đạt",
                            CreatedBy = obj.CreatedBy,
                            CreatedDate = now
                        };
                        await _inventoryTransactionRepository.CreateAsync(txQuarantine);
                    }
                    await _inventoryTransactionRepository.SaveChangesAsync();
                }

                lot.LastModifiedDate = now;
                lot.UpdatedBy = obj.CreatedBy;
                await _paddyLotRepository.UpdateAsync(lot);
                await _paddyLotRepository.SaveChangesAsync();
            }

            // 6. Xử lý Tồn kho (Inventory) và Giao dịch tồn kho (InventoryTransaction) nếu xảy ra Tách lô
            if (isSplit && childLot != null)
            {
                decimal remainingToDeduct = obj.AffectedWeightKg!.Value;

                // B2. Sắp xếp trừ trên phần khả dụng (QuantityOnHand - QuantityReserved)
                foreach (var parentInv in parentInventories.OrderByDescending(x => x.QuantityOnHand - x.QuantityReserved))
                {
                    if (remainingToDeduct <= 0) break;

                    decimal parentAvailable = Math.Max(0, parentInv.QuantityOnHand - parentInv.QuantityReserved);
                    decimal deductQty = Math.Min(parentAvailable, remainingToDeduct);
                    if (deductQty <= 0) continue;

                    parentInv.QuantityOnHand -= deductQty;
                    parentInv.LastModifiedDate = now;
                    parentInv.UpdatedBy = obj.CreatedBy;
                    await _inventoryRepository.UpdateAsync(parentInv);

                    // Kiểm tra tồn tại tồn kho của lô con tại cùng vị trí để tránh trùng lắp dòng
                    var childInv = await _inventoryRepository.FirstOrDefaultAsync(x => x.PaddyLotId == childLot.Id 
                        && x.WarehouseId == parentInv.WarehouseId 
                        && x.LocationId == parentInv.LocationId 
                        && !x.IsDeleted);

                    if (childInv != null)
                    {
                        childInv.QuantityOnHand += deductQty;
                        childInv.LastModifiedDate = now;
                        childInv.UpdatedBy = obj.CreatedBy;
                        await _inventoryRepository.UpdateAsync(childInv);
                    }
                    else
                    {
                        childInv = new Inventory
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
                    }
                    await _inventoryRepository.SaveChangesAsync();

                    remainingToDeduct -= deductQty;

                    // Ghi nhận giao dịch điều chỉnh tồn kho cho lô gốc (Dùng MANUAL_ADJUST - B5)
                    var txOut = new InventoryTransaction
                    {
                        InventoryId = parentInv.Id,
                        WarehouseId = parentInv.WarehouseId,
                        LocationId = parentInv.LocationId,
                        ProductVariantId = parentInv.ProductVariantId,
                        PaddyLotId = lot.Id,
                        TransactionType = InventoryTransactionTypeConstants.ManualAdjust,
                        ReferenceType = InventoryReferenceTypeConstants.QualityInspectionSplit,
                        ReferenceId = entity.Id,
                        Quantity = deductQty,
                        BeforeQuantity = parentInv.QuantityOnHand + deductQty,
                        AfterQuantity = parentInv.QuantityOnHand,
                        Note = $"Trừ tồn do tách lô cách ly {childLot.LotCode}",
                        CreatedBy = obj.CreatedBy,
                        CreatedDate = now
                    };
                    await _inventoryTransactionRepository.CreateAsync(txOut);

                    // Ghi nhận giao dịch điều chỉnh tồn kho cho lô con (Dùng MANUAL_ADJUST - B5)
                    var txIn = new InventoryTransaction
                    {
                        InventoryId = childInv.Id,
                        WarehouseId = childInv.WarehouseId,
                        LocationId = childInv.LocationId,
                        ProductVariantId = parentInv.ProductVariantId,
                        PaddyLotId = childLot.Id,
                        TransactionType = InventoryTransactionTypeConstants.ManualAdjust,
                        ReferenceType = InventoryReferenceTypeConstants.QualityInspectionSplit,
                        ReferenceId = entity.Id,
                        Quantity = deductQty,
                        BeforeQuantity = childInv.QuantityOnHand - deductQty,
                        AfterQuantity = childInv.QuantityOnHand,
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

        // B7. Khóa không cho phép sửa kết quả hoặc khối lượng của phiếu đã tách lô cách ly
        bool wasSplit = !entity.PassedInspection && entity.AffectedWeightKg.HasValue && entity.AffectedWeightKg.Value > 0;
        if (wasSplit)
        {
            if (entity.PaddyLotId != obj.PaddyLotId ||
                entity.PassedInspection != obj.PassedInspection ||
                entity.AffectedWeightKg != obj.AffectedWeightKg)
            {
                return ApiResponse.BadRequest(message: "Không thể thay đổi Lô lúa/gạo, Kết quả kiểm định hoặc Khối lượng ảnh hưởng của phiếu kiểm định đã thực hiện tách lô cách ly.");
            }
        }

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
        // C2: Nếu phiếu đã tách lô cách ly (wasSplit = true), BỎ QUA việc ghi đè trạng thái lô gốc.
        if (!wasSplit)
        {
            var lot = await _paddyLotRepository.GetByIdAsync(obj.PaddyLotId);
            if (lot != null && !lot.IsDeleted)
            {
                lot.QualityStatus = obj.PassedInspection ? QualityStatusConstants.Passed : QualityStatusConstants.Failed;
                lot.LastModifiedDate = entity.LastModifiedDate;
                await _paddyLotRepository.UpdateAsync(lot);
                await _paddyLotRepository.SaveChangesAsync();
            }
        }

        return ApiResponse.Success(entity.Id, "Cập nhật phiếu kiểm tra thành công.");
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateQualityInspectionDto> objs)
        => Task.FromResult(ApiResponse.Error(message: "UpdateList chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null || entity.IsDeleted) return ApiResponse.NotFound();

        // B7. Khóa không cho xóa phiếu đã tách lô cách ly
        bool wasSplit = !entity.PassedInspection && entity.AffectedWeightKg.HasValue && entity.AffectedWeightKg.Value > 0;
        if (wasSplit)
        {
            return ApiResponse.BadRequest(message: "Không thể xóa phiếu kiểm định đã thực hiện tách lô cách ly.");
        }

        var isDeleted = await _repo.SoftDeleteAsync(id);
        if (!isDeleted) return ApiResponse.BadRequest();
        await _repo.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        foreach (var id in objs)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity != null)
            {
                bool wasSplit = !entity.PassedInspection && entity.AffectedWeightKg.HasValue && entity.AffectedWeightKg.Value > 0;
                if (wasSplit)
                {
                    return ApiResponse.BadRequest(message: $"Không thể xóa phiếu kiểm định (ID: {id}) đã thực hiện tách lô cách ly.");
                }
            }
        }

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
