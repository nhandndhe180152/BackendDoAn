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
using Backend.Application.BackgroundJobs.LotQualityRecheck;
using Backend.Share.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ kiểm tra chất lượng lô lúa/gạo (QualityInspection).
/// </summary>
public class QualityInspectionService : IQualityInspectionService
{
    private readonly IQualityInspectionRepository _repo;
    private readonly IPaddyLotRepository _paddyLotRepository;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryTransactionRepository _inventoryTransactionRepository;
    private readonly IRepositoryBase<LotStatus, int> _lotStatusRepository;
    private readonly IApplicationDbContext _context;
    private readonly IScheduledJobService _scheduledJobService;

    public QualityInspectionService(
        IQualityInspectionRepository repo,
        IPaddyLotRepository paddyLotRepository,
        IInventoryRepository inventoryRepository,
        IInventoryTransactionRepository inventoryTransactionRepository,
        IRepositoryBase<LotStatus, int> lotStatusRepository,
        IApplicationDbContext context,
        INotificationDispatcher notificationDispatcher,
        IScheduledJobService? scheduledJobService = null)
    {
        _repo = repo;
        _paddyLotRepository = paddyLotRepository;
        _notificationDispatcher = notificationDispatcher;
        _inventoryRepository = inventoryRepository;
        _inventoryTransactionRepository = inventoryTransactionRepository;
        _lotStatusRepository = lotStatusRepository;
        _context = context;
        _scheduledJobService = scheduledJobService;
    }

    public async Task<ApiResponse> CreateAsync(CreateQualityInspectionDto obj)
    {
        var lot = await _paddyLotRepository.GetByIdAsync(obj.PaddyLotId);
        if (lot == null || lot.IsDeleted)
            return ApiResponse.NotFound(message: "Không tìm thấy lô lúa/gạo.");

        var selectedBagIds = (obj.AffectedBagIds ?? new List<int>()).Distinct().ToList();
        if (obj.PassedInspection && selectedBagIds.Count > 0)
            return ApiResponse.BadRequest(message: "Không được chọn bao cách ly khi kết quả kiểm định là Đạt.");
        if (!obj.PassedInspection && obj.AffectedWeightKg.HasValue && selectedBagIds.Count == 0)
            return ApiResponse.BadRequest(message: "Không nhập kg ảnh hưởng thủ công. Hãy chọn chính xác các bao cần tách sang cách ly.");
        if (selectedBagIds.Count > 0)
        {
            var selectedBags = await _context.PaddyLotBags
                .Include(x => x.Contents)
                .Where(x => selectedBagIds.Contains(x.Id) && x.LotId == lot.Id && !x.IsDeleted
                    && x.Status == PaddyLotBagStatuses.Pending)
                .ToListAsync();
            if (selectedBags.Count != selectedBagIds.Count)
                return ApiResponse.BadRequest(message: "Có bao không thuộc lô hoặc không còn ở trạng thái chờ nhập để tách cách ly.");
            if (selectedBags.Any(x => x.Contents
                .Any(c => !c.IsDeleted && c.WeightKg > 0 && c.LotId != lot.Id)))
                return ApiResponse.BadRequest(message: "Không thể tách cách ly bao hỗn hợp bằng kiểm định của một lô. Hãy xử lý cách ly toàn bộ bao hỗn hợp theo từng thành phần lô.");
            obj.AffectedWeightKg = selectedBags.Sum(x => x.WeightKg);
        }

        // Trọng lượng cơ sở của lô: lô đã nhập kho dùng RemainingWeightKg; lô CHƯA nhập kho
        // (đang chờ kiểm định, RemainingWeightKg = 0) dùng InitialWeightKg.
        decimal lotWeight = lot.RemainingWeightKg > 0 ? lot.RemainingWeightKg : lot.InitialWeightKg;

        // B8. Validate đầu vào AffectedWeightKg
        if (obj.AffectedWeightKg.HasValue)
        {
            if (obj.AffectedWeightKg.Value < 0)
                return ApiResponse.BadRequest(message: "Khối lượng bị ảnh hưởng không được nhỏ hơn 0.");
            if (obj.AffectedWeightKg.Value == 0)
                return ApiResponse.BadRequest(message: "Khối lượng bị ảnh hưởng phải lớn hơn 0.");
            if (obj.AffectedWeightKg.Value >= lotWeight)
                return ApiResponse.BadRequest(message: $"Khối lượng bị ảnh hưởng ({obj.AffectedWeightKg.Value} kg) phải nhỏ hơn khối lượng của lô hàng ({lotWeight} kg).");
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

            // Lô đã nhập kho (có tồn) tách theo tồn kho; lô CHƯA nhập kho tách ở mức lô (không có tồn để trừ).
            bool hasInventory = parentInventories.Any();
            bool isPartialQuarantine = !obj.PassedInspection
                && obj.AffectedWeightKg.HasValue
                && obj.AffectedWeightKg.Value > 0
                && obj.AffectedWeightKg.Value < lotWeight;

            // ── (1) Lô CHƯA nhập kho + không đạt 1 phần: tách LÔ (không đụng tồn kho), tạo thêm
            //        dòng phiếu nhập cho lô con để màn Nhập kho gợi ý Ô CÁCH LY cho phần không đạt,
            //        đồng thời vẫn có ô riêng cho phần gạo/lúa đạt (lô cha).
            if (isPartialQuarantine && !hasInventory)
            {
                isSplit = true;

                await _repo.CreateAsync(entity);
                await _repo.SaveChangesAsync();

                var quarantineStatusPs = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == LotStatusCodeConstants.Quarantine && !x.IsDeleted);
                if (quarantineStatusPs == null)
                    throw new InvalidOperationException("Không tìm thấy trạng thái LotStatus 'QUARANTINE' trong hệ thống.");
                var pendingInboundStatusPs = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == LotStatusCodeConstants.PendingInbound && !x.IsDeleted);

                string childLotCode = $"{lot.LotCode}-Q1";
                int qIndex = 1;
                while (await _context.PaddyLots.AnyAsync(x => x.LotCode == childLotCode && x.OrganizationId == lot.OrganizationId))
                {
                    qIndex++;
                    childLotCode = $"{lot.LotCode}-Q{qIndex}";
                }

                childLot = new PaddyLot
                {
                    OrganizationId = lot.OrganizationId,
                    LotCode = childLotCode,
                    LotType = lot.LotType,
                    ProductVariantId = lot.ProductVariantId,
                    RiceVarietyId = lot.RiceVarietyId,
                    StatusId = quarantineStatusPs.Id,
                    WarehouseId = lot.WarehouseId,
                    LocationId = lot.LocationId,
                    InboundDate = lot.InboundDate,
                    InitialWeightKg = obj.AffectedWeightKg!.Value,
                    RemainingWeightKg = 0m, // chưa nhập kho — tồn sẽ tăng khi xác nhận xếp kho
                    CostPricePerKg = lot.CostPricePerKg,
                    QualityStatus = QualityStatusConstants.Failed,
                    QrCode = "PL-" + Guid.NewGuid().ToString("N").ToUpper(),
                    QrImageUrl = lot.QrImageUrl,
                    ParentLotId = lot.Id,
                    SourceReceiptId = null,
                    SourceMillingOrderId = lot.SourceMillingOrderId,
                    CreatedBy = obj.CreatedBy,
                    CreatedDate = now
                };
                await _paddyLotRepository.CreateAsync(childLot);
                await _paddyLotRepository.SaveChangesAsync();

                // Lô cha: giữ phần đạt, chờ nhập kho bình thường.
                lot.InitialWeightKg -= obj.AffectedWeightKg.Value;
                lot.QualityStatus = QualityStatusConstants.Passed;
                if (pendingInboundStatusPs != null) lot.StatusId = pendingInboundStatusPs.Id;
                lot.LastModifiedDate = now;
                lot.UpdatedBy = obj.CreatedBy;
                await _paddyLotRepository.UpdateAsync(lot);
                await _paddyLotRepository.SaveChangesAsync();

                // Phiếu nhập kho (2 dòng: lô cha đạt + lô con cách ly) được tạo ở cuối (khi lô chưa nhập kho & chưa có phiếu nhập).
            }
            // ── (2) Lô ĐÃ nhập kho + không đạt 1 phần: tách theo tồn kho (luồng cũ).
            else if (isPartialQuarantine && hasInventory)
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
                    // SourceReceipt là quan hệ 1-1 (unique index IX_PaddyLot_SourceReceiptId):
                    // lô cha đã giữ FK này nên lô con tách cách ly KHÔNG copy để tránh trùng khóa.
                    // Truy vết nguồn gốc của lô con đi qua ParentLotId -> lô cha -> SourceReceiptId.
                    SourceReceiptId = null,
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

                // Kiểm định ĐẠT: lô đã nhập kho → IN_STOCK; lô CHƯA nhập kho → PENDING_INBOUND
                // (hiện ở màn Nhập kho để xếp vị trí, chỉ thành IN_STOCK sau khi xác nhận nhập kho).
                if (obj.PassedInspection)
                {
                    var passedCode = hasInventory ? LotStatusCodeConstants.InStock : LotStatusCodeConstants.PendingInbound;
                    var passedStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == passedCode && !x.IsDeleted);
                    if (passedStatus != null)
                    {
                        lot.StatusId = passedStatus.Id;
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
                        await _inventoryTransactionRepository.CreateWithColumnTotalsAsync(txQuarantine);
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
                await MoveSelectedBagsToQuarantineLotAsync(
                    lot.Id, childLot.Id, selectedBagIds, entity.Id, obj.CreatedBy, now);
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
                    await _inventoryTransactionRepository.CreateWithColumnTotalsAsync(txOut);

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
                    await _inventoryTransactionRepository.CreateWithColumnTotalsAsync(txIn);
                }

                await _inventoryTransactionRepository.SaveChangesAsync();
            }

            // Lô CHƯA nhập kho và CHƯA có phiếu nhập: sau khi kiểm định xong tạo phiếu nhập kho
            // (Draft) để đưa vào màn Nhập kho. 1 dòng cho lô (đạt / cách ly toàn bộ); 2 dòng khi
            // tách 1 phần (lô cha đạt + lô con cách ly) → màn Nhập kho gợi ý ô thường & ô cách ly.
            // Điều kiện không phụ thuộc trạng thái AWAITING_QC nên vẫn đúng nếu chưa thêm trạng thái đó.
            bool lotAlreadyHasInbound = await _context.InboundOrderItems
                .AnyAsync(i => i.PaddyLotId == lot.Id && !i.IsDeleted);
            if (!hasInventory && !lotAlreadyHasInbound)
            {
                var lines = new List<InboundLine>
                {
                    new(lot.Id, lot.ProductVariantId, lot.InitialWeightKg, lot.CostPricePerKg)
                };
                if (childLot != null)
                    lines.Add(new(childLot.Id, childLot.ProductVariantId, childLot.InitialWeightKg, childLot.CostPricePerKg));
                await CreateReceiptInboundOrderAsync(lot, lines, obj.CreatedBy, now);
            }

            await tx.CommitAsync();

            try
            {
                if (_scheduledJobService != null)
                {
                    _scheduledJobService.Enqueue<ILotQualityRecheckService>(s => s.EvaluateLotAsync(entity.PaddyLotId, CancellationToken.None));
                    if (isSplit && childLot != null)
                    {
                        _scheduledJobService.Enqueue<ILotQualityRecheckService>(s => s.EvaluateLotAsync(childLot.Id, CancellationToken.None));
                    }
                }
            }
            catch
            {
                // Ignore to avoid disrupting business flow
            }
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        // Giao phiếu cho người kiểm định -> thông báo + push FCM cho người được giao.
        // (Không tự gửi lại cho chính người tạo nếu tự nhận kiểm.)
        if (entity.InspectorId.HasValue && entity.InspectorId != obj.CreatedBy)
        {
            await _notificationDispatcher.DispatchAsync(
                NotificationConstants.Code.QualityInspectionAssigned,
                new NotificationTarget { UserIds = new List<int> { entity.InspectorId.Value } },
                new object[] { lot.LotCode },
                "/admin/quality-inspections",
                obj.CreatedBy);
        }

        return ApiResponse.Created(entity.Id, "Tạo phiếu kiểm tra chất lượng thành công.");
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateQualityInspectionDto> objs)
        => Task.FromResult(ApiResponse.Error(message: "CreateList chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> GetAllAsync()
    {
        var entities = await _repo
            .FindByCondition(x => !x.IsDeleted, false, x => x.PaddyLot)
            .Include(x => x.PaddyLot).ThenInclude(p => p.Status)
            .OrderByDescending(x => x.InspectedAt)
            .ToListAsync();

        return ApiResponse.Success(entities.Select(ToDto).ToList());
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var entity = await _repo
            .FindByCondition(x => x.Id == id && !x.IsDeleted, false, x => x.PaddyLot)
            .Include(x => x.PaddyLot).ThenInclude(p => p.Status)
            .FirstOrDefaultAsync();

        if (entity == null) return ApiResponse.NotFound();
        return ApiResponse.Success(ToDto(entity));
    }

    public async Task<ApiResponse> GetByLotAsync(int paddyLotId)
    {
        var entities = await _repo
            .FindByCondition(x => x.PaddyLotId == paddyLotId && !x.IsDeleted, false, x => x.PaddyLot)
            .Include(x => x.PaddyLot).ThenInclude(p => p.Status)
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
            // Preserve the original split inspection as audit history. When the edit
            // screen marks it as passed, recheck the quarantine child instead.
            if (obj.PassedInspection && !entity.PassedInspection)
            {
                var quarantineChildren = await _context.PaddyLots
                    .Include(x => x.Status)
                    .Where(x => x.ParentLotId == entity.PaddyLotId && !x.IsDeleted
                        && x.Status != null && x.Status.Code == LotStatusCodeConstants.Quarantine)
                    .ToListAsync();

                if (quarantineChildren.Count != 1)
                    return ApiResponse.BadRequest(message: quarantineChildren.Count == 0
                        ? "Không thể thay đổi Lô lúa/gạo, Kết quả kiểm định hoặc Khối lượng ảnh hưởng vì không tìm thấy lô con đang cách ly để kiểm tra lại."
                        : "Có nhiều lô con đang cách ly. Vui lòng chọn đúng lô cách ly để kiểm tra lại.");

                return await RecheckAsync(new CreateQualityInspectionDto
                {
                    PaddyLotId = quarantineChildren[0].Id,
                    InspectorId = obj.InspectorId,
                    InspectedAt = obj.InspectedAt,
                    MoisturePercent = obj.MoisturePercent,
                    ImpurityPercent = obj.ImpurityPercent,
                    MoldLevel = obj.MoldLevel,
                    PestLevel = obj.PestLevel,
                    PackagingStatus = obj.PackagingStatus,
                    PassedInspection = true,
                    Handling = obj.Handling,
                    Note = obj.Note,
                    CreatedBy = obj.UpdatedBy
                });
            }

            if (entity.PaddyLotId != obj.PaddyLotId ||
                entity.PassedInspection != obj.PassedInspection ||
                entity.AffectedWeightKg != obj.AffectedWeightKg)
            {
                return ApiResponse.BadRequest(message: "Không thể thay đổi Lô lúa/gạo, Kết quả kiểm định hoặc Khối lượng ảnh hưởng của phiếu kiểm định đã thực hiện tách lô cách ly.");
            }
        }

        var lot = await _paddyLotRepository.GetByIdAsync(obj.PaddyLotId);
        if (lot == null || lot.IsDeleted)
            return ApiResponse.NotFound(message: "Không tìm thấy lô lúa/gạo.");

        var selectedBagIds = (obj.AffectedBagIds ?? new List<int>()).Distinct().ToList();
        if (!wasSplit && obj.PassedInspection && selectedBagIds.Count > 0)
            return ApiResponse.BadRequest(message: "Không được chọn bao cách ly khi kết quả kiểm định là Đạt.");
        if (!wasSplit && !obj.PassedInspection && obj.AffectedWeightKg.HasValue && selectedBagIds.Count == 0)
            return ApiResponse.BadRequest(message: "Không nhập kg ảnh hưởng thủ công. Hãy chọn chính xác các bao cần tách sang cách ly.");
        if (!wasSplit && selectedBagIds.Count > 0)
        {
            var selectedBags = await _context.PaddyLotBags
                .Include(x => x.Contents)
                .Where(x => selectedBagIds.Contains(x.Id) && x.LotId == lot.Id && !x.IsDeleted
                    && x.Status == PaddyLotBagStatuses.Pending)
                .ToListAsync();
            if (selectedBags.Count != selectedBagIds.Count)
                return ApiResponse.BadRequest(message: "Có bao không thuộc lô hoặc không còn ở trạng thái chờ nhập để tách cách ly.");
            if (selectedBags.Any(x => x.Contents
                .Any(c => !c.IsDeleted && c.WeightKg > 0 && c.LotId != lot.Id)))
                return ApiResponse.BadRequest(message: "Không thể tách cách ly bao hỗn hợp bằng kiểm định của một lô. Hãy xử lý cách ly toàn bộ bao hỗn hợp theo từng thành phần lô.");
            obj.AffectedWeightKg = selectedBags.Sum(x => x.WeightKg);
        }

        // Trọng lượng cơ sở: lô đã nhập kho dùng RemainingWeightKg; lô chưa nhập kho dùng InitialWeightKg.
        decimal lotWeight = lot.RemainingWeightKg > 0 ? lot.RemainingWeightKg : lot.InitialWeightKg;

        // B8. Validate đầu vào AffectedWeightKg
        if (!wasSplit && obj.AffectedWeightKg.HasValue)
        {
            if (obj.AffectedWeightKg.Value < 0)
                return ApiResponse.BadRequest(message: "Khối lượng bị ảnh hưởng không được nhỏ hơn 0.");
            if (obj.AffectedWeightKg.Value == 0)
                return ApiResponse.BadRequest(message: "Khối lượng bị ảnh hưởng phải lớn hơn 0.");
            if (obj.AffectedWeightKg.Value >= lotWeight)
                return ApiResponse.BadRequest(message: $"Khối lượng bị ảnh hưởng ({obj.AffectedWeightKg.Value} kg) phải nhỏ hơn khối lượng của lô hàng ({lotWeight} kg).");
        }

        bool isSplit = false;
        PaddyLot? childLot = null;
        var now = DateTimeHelper.VietnamNow();

        await using var tx = await _repo.BeginTransactionAsync();
        try
        {
            var parentInventories = await _inventoryRepository
                .FindByCondition(x => x.PaddyLotId == lot.Id && !x.IsDeleted)
                .ToListAsync();

            bool hasInventory = parentInventories.Any();
            bool isPartialQuarantine = !wasSplit && !obj.PassedInspection
                && obj.AffectedWeightKg.HasValue
                && obj.AffectedWeightKg.Value > 0
                && obj.AffectedWeightKg.Value < lotWeight;

            // ── (1) Lô CHƯA nhập kho + không đạt 1 phần: tách LÔ + thêm dòng phiếu nhập cho lô con.
            if (isPartialQuarantine && !hasInventory)
            {
                isSplit = true;

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
                entity.LastModifiedDate = now;
                entity.AffectedWeightKg = obj.AffectedWeightKg;
                await _repo.UpdateAsync(entity);
                await _repo.SaveChangesAsync();

                var quarantineStatusPs = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == LotStatusCodeConstants.Quarantine && !x.IsDeleted);
                if (quarantineStatusPs == null)
                    throw new InvalidOperationException("Không tìm thấy trạng thái LotStatus 'QUARANTINE' trong hệ thống.");
                var pendingInboundStatusPs = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == LotStatusCodeConstants.PendingInbound && !x.IsDeleted);

                string childCode = $"{lot.LotCode}-Q1";
                int qIdx = 1;
                while (await _context.PaddyLots.AsNoTracking().AnyAsync(x => x.LotCode == childCode && x.OrganizationId == lot.OrganizationId))
                {
                    qIdx++;
                    childCode = $"{lot.LotCode}-Q{qIdx}";
                }

                childLot = new PaddyLot
                {
                    OrganizationId = lot.OrganizationId,
                    LotCode = childCode,
                    LotType = lot.LotType,
                    ProductVariantId = lot.ProductVariantId,
                    RiceVarietyId = lot.RiceVarietyId,
                    StatusId = quarantineStatusPs.Id,
                    WarehouseId = lot.WarehouseId,
                    LocationId = lot.LocationId,
                    InboundDate = lot.InboundDate,
                    InitialWeightKg = obj.AffectedWeightKg!.Value,
                    RemainingWeightKg = 0m,
                    CostPricePerKg = lot.CostPricePerKg,
                    QualityStatus = QualityStatusConstants.Failed,
                    QrCode = "PL-" + Guid.NewGuid().ToString("N").ToUpper(),
                    QrImageUrl = lot.QrImageUrl,
                    ParentLotId = lot.Id,
                    SourceReceiptId = null,
                    SourceMillingOrderId = lot.SourceMillingOrderId,
                    CreatedBy = obj.UpdatedBy,
                    CreatedDate = now
                };
                await _paddyLotRepository.CreateAsync(childLot);
                await _paddyLotRepository.SaveChangesAsync();

                lot.InitialWeightKg -= obj.AffectedWeightKg.Value;
                lot.QualityStatus = QualityStatusConstants.Passed;
                if (pendingInboundStatusPs != null) lot.StatusId = pendingInboundStatusPs.Id;
                lot.LastModifiedDate = now;
                lot.UpdatedBy = obj.UpdatedBy;
                await _paddyLotRepository.UpdateAsync(lot);
                await _paddyLotRepository.SaveChangesAsync();

                // Phiếu nhập kho (2 dòng: lô cha đạt + lô con cách ly) được tạo ở cuối (khi lô chưa nhập kho & chưa có phiếu nhập).
            }
            // ── (2) Lô ĐÃ nhập kho + không đạt 1 phần: tách theo tồn kho (luồng cũ).
            else if (isPartialQuarantine && hasInventory)
            {
                // B1. Kiểm tra tổng tồn kho khả dụng trước khi tách
                decimal totalAvailable = parentInventories.Sum(x => Math.Max(0, x.QuantityOnHand - x.QuantityReserved));
                if (totalAvailable < obj.AffectedWeightKg.Value)
                {
                    return ApiResponse.BadRequest(message: $"Không đủ tồn kho khả dụng để thực hiện tách lô cách ly (Tồn kho khả dụng: {totalAvailable} kg, Cần tách: {obj.AffectedWeightKg.Value} kg).");
                }

                isSplit = true;

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
                entity.LastModifiedDate = now;
                entity.AffectedWeightKg = obj.AffectedWeightKg;

                await _repo.UpdateAsync(entity);
                await _repo.SaveChangesAsync();

                // 1. Tạo LotCode mới: nối đuôi -Q1, -Q2,...
                string childLotCode = $"{lot.LotCode}-Q1";
                int quarantineIndex = 1;
                while (await _context.PaddyLots.AsNoTracking().AnyAsync(x => x.LotCode == childLotCode && x.OrganizationId == lot.OrganizationId))
                {
                    quarantineIndex++;
                    childLotCode = $"{lot.LotCode}-Q{quarantineIndex}";
                }

                // 2. Tìm trạng thái QUARANTINE cho lô mới
                var quarantineStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == LotStatusCodeConstants.Quarantine && !x.IsDeleted);
                if (quarantineStatus == null)
                    throw new InvalidOperationException("Không tìm thấy trạng thái LotStatus 'QUARANTINE' trong hệ thống.");

                // 3. Khởi tạo lô con (childLot) và copy các trường truy xuất nguồn gốc
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
                    QualityStatus = QualityStatusConstants.Failed,
                    QrCode = "PL-" + Guid.NewGuid().ToString("N").ToUpper(),
                    QrImageUrl = lot.QrImageUrl,
                    ParentLotId = lot.Id,
                    SourceReceiptId = null,
                    SourceMillingOrderId = lot.SourceMillingOrderId,
                    CreatedBy = obj.UpdatedBy,
                    CreatedDate = now
                };

                await _paddyLotRepository.CreateAsync(childLot);
                await _paddyLotRepository.SaveChangesAsync();

                // 4. Trừ số lượng ở lô gốc
                lot.RemainingWeightKg -= obj.AffectedWeightKg.Value;
                lot.QualityStatus = QualityStatusConstants.Passed; // Lô gốc phần còn lại là Đạt chất lượng

                var inStockStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == LotStatusCodeConstants.InStock && !x.IsDeleted);
                if (inStockStatus != null && lot.StatusId != inStockStatus.Id)
                {
                    lot.StatusId = inStockStatus.Id;
                }

                lot.LastModifiedDate = now;
                lot.UpdatedBy = obj.UpdatedBy;
                await _paddyLotRepository.UpdateAsync(lot);
                await _paddyLotRepository.SaveChangesAsync();
            }
            else
            {
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
                entity.LastModifiedDate = now;
                entity.AffectedWeightKg = obj.AffectedWeightKg;

                await _repo.UpdateAsync(entity);
                await _repo.SaveChangesAsync();

                // Luồng All-or-nothing cũ
                if (!wasSplit)
                {
                    lot.QualityStatus = obj.PassedInspection ? QualityStatusConstants.Passed : QualityStatusConstants.Failed;

                    if (obj.PassedInspection)
                    {
                        // Lô đã nhập kho → IN_STOCK; lô chưa nhập kho → PENDING_INBOUND (đợi xếp kho).
                        var passedCode = hasInventory ? LotStatusCodeConstants.InStock : LotStatusCodeConstants.PendingInbound;
                        var passedStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == passedCode && !x.IsDeleted);
                        if (passedStatus != null)
                        {
                            lot.StatusId = passedStatus.Id;
                        }
                    }
                    else
                    {
                        var quarantineStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == LotStatusCodeConstants.Quarantine && !x.IsDeleted);
                        if (quarantineStatus != null)
                        {
                            lot.StatusId = quarantineStatus.Id;
                        }

                        // Ghi nhận giao dịch điều chỉnh khi cả lô fail -> QUARANTINE để nhất quán tồn kho
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
                                Quantity = 0,
                                BeforeQuantity = inv.QuantityOnHand,
                                AfterQuantity = inv.QuantityOnHand,
                                Note = "Toàn bộ lô hàng chuyển sang trạng thái CÁCH LY do kiểm định không đạt",
                                CreatedBy = obj.UpdatedBy,
                                CreatedDate = now
                            };
                            await _inventoryTransactionRepository.CreateWithColumnTotalsAsync(txQuarantine);
                        }
                        await _inventoryTransactionRepository.SaveChangesAsync();
                    }

                    lot.LastModifiedDate = now;
                    lot.UpdatedBy = obj.UpdatedBy;
                    await _paddyLotRepository.UpdateAsync(lot);
                    await _paddyLotRepository.SaveChangesAsync();
                }
            }

            // 6. Xử lý Tồn kho (Inventory) và Giao dịch tồn kho (InventoryTransaction) nếu xảy ra Tách lô
            if (isSplit && childLot != null)
            {
                await MoveSelectedBagsToQuarantineLotAsync(
                    lot.Id, childLot.Id, selectedBagIds, entity.Id, obj.UpdatedBy, now);
                decimal remainingToDeduct = obj.AffectedWeightKg!.Value;

                foreach (var parentInv in parentInventories.OrderByDescending(x => x.QuantityOnHand - x.QuantityReserved))
                {
                    if (remainingToDeduct <= 0) break;

                    decimal parentAvailable = Math.Max(0, parentInv.QuantityOnHand - parentInv.QuantityReserved);
                    decimal deductQty = Math.Min(parentAvailable, remainingToDeduct);
                    if (deductQty <= 0) continue;

                    parentInv.QuantityOnHand -= deductQty;
                    parentInv.LastModifiedDate = now;
                    parentInv.UpdatedBy = obj.UpdatedBy;
                    await _inventoryRepository.UpdateAsync(parentInv);

                    var childInv = await _inventoryRepository.FirstOrDefaultAsync(x => x.PaddyLotId == childLot.Id 
                        && x.WarehouseId == parentInv.WarehouseId 
                        && x.LocationId == parentInv.LocationId 
                        && !x.IsDeleted);

                    if (childInv != null)
                    {
                        childInv.QuantityOnHand += deductQty;
                        childInv.LastModifiedDate = now;
                        childInv.UpdatedBy = obj.UpdatedBy;
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
                            CreatedBy = obj.UpdatedBy
                        };
                        await _inventoryRepository.CreateAsync(childInv);
                    }
                    await _inventoryRepository.SaveChangesAsync();

                    remainingToDeduct -= deductQty;

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
                        CreatedBy = obj.UpdatedBy,
                        CreatedDate = now
                    };
                    await _inventoryTransactionRepository.CreateWithColumnTotalsAsync(txOut);

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
                        CreatedBy = obj.UpdatedBy,
                        CreatedDate = now
                    };
                    await _inventoryTransactionRepository.CreateWithColumnTotalsAsync(txIn);
                }

                await _inventoryTransactionRepository.SaveChangesAsync();
            }

            // Lô CHƯA nhập kho và CHƯA có phiếu nhập: sau khi kiểm định xong tạo phiếu nhập kho (Draft).
            bool lotAlreadyHasInbound = await _context.InboundOrderItems
                .AnyAsync(i => i.PaddyLotId == lot.Id && !i.IsDeleted);
            if (!hasInventory && !lotAlreadyHasInbound)
            {
                var lines = new List<InboundLine>
                {
                    new(lot.Id, lot.ProductVariantId, lot.InitialWeightKg, lot.CostPricePerKg)
                };
                if (childLot != null)
                    lines.Add(new(childLot.Id, childLot.ProductVariantId, childLot.InitialWeightKg, childLot.CostPricePerKg));
                await CreateReceiptInboundOrderAsync(lot, lines, obj.UpdatedBy, now);
            }

            await tx.CommitAsync();

            try
            {
                if (_scheduledJobService != null)
                {
                    _scheduledJobService.Enqueue<ILotQualityRecheckService>(s => s.EvaluateLotAsync(entity.PaddyLotId, CancellationToken.None));
                    if (isSplit && childLot != null)
                    {
                        _scheduledJobService.Enqueue<ILotQualityRecheckService>(s => s.EvaluateLotAsync(childLot.Id, CancellationToken.None));
                    }
                }
            }
            catch
            {
                // Ignore to avoid disrupting business flow
            }
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return ApiResponse.Success(entity.Id, "Cập nhật phiếu kiểm tra thành công.");
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateQualityInspectionDto> objs)
        => Task.FromResult(ApiResponse.Error(message: "UpdateList chưa được hỗ trợ.", status: 501));

    /// <summary>
    /// KIỂM TRA LẠI chất lượng cho một lô đang CÁCH LY (QUARANTINE).
    /// Nếu ĐẠT: ghi phiếu kiểm định, rút TOÀN BỘ tồn ra khỏi (các) ô cách ly, đưa lô về trạng thái
    /// CHỜ NHẬP KHO (PENDING_INBOUND) và sinh phiếu nhập kho (Draft) để màn Store-in xếp lại vào ô
    /// thường (put-away gợi ý ô KHÔNG cách ly vì QualityStatus đã là Passed).
    /// Nếu KHÔNG ĐẠT: chỉ ghi nhận phiếu kiểm định, lô vẫn ở trạng thái CÁCH LY.
    /// </summary>
    public async Task<ApiResponse> RecheckAsync(CreateQualityInspectionDto obj)
    {
        var lot = await _paddyLotRepository
            .FindByCondition(x => x.Id == obj.PaddyLotId && !x.IsDeleted, false, x => x.Status)
            .FirstOrDefaultAsync();
        if (lot == null)
            return ApiResponse.NotFound(message: "Không tìm thấy lô lúa/gạo.");

        // Chỉ cho tái kiểm lô đang CÁCH LY.
        if (lot.Status?.Code != LotStatusCodeConstants.Quarantine)
            return ApiResponse.BadRequest(message: "Chỉ được kiểm tra lại chất lượng cho lô đang ở trạng thái CÁCH LY.");

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
            AffectedWeightKg = null // tái kiểm xử lý toàn bộ lô, không tách 1 phần
        };

        await using var tx = await _repo.BeginTransactionAsync();
        try
        {
            await _repo.CreateAsync(entity);
            await _repo.SaveChangesAsync();

            if (obj.PassedInspection)
            {
                // 1. Rút toàn bộ tồn ra khỏi (các) ô cách ly của lô.
                var inventories = await _inventoryRepository
                    .FindByCondition(x => x.PaddyLotId == lot.Id && !x.IsDeleted)
                    .ToListAsync();

                var storedBags = await _context.PaddyLotBags
                    .Where(x => x.LotId == lot.Id && !x.IsDeleted
                        && x.Status == PaddyLotBagStatuses.Stored)
                    .ToListAsync();
                if (storedBags.Any(x => !x.LocationId.HasValue))
                    return ApiResponse.BadRequest(message: "Dữ liệu bao cách ly không hợp lệ: có bao đang lưu kho nhưng không có vị trí.");

                var storedBagWeight = storedBags.Sum(x => x.WeightKg);
                var inventoryWeight = inventories.Sum(x => x.QuantityOnHand);
                if (storedBags.Count > 0 && Math.Abs(storedBagWeight - inventoryWeight) > 0.001m)
                    return ApiResponse.BadRequest(message: $"Dữ liệu bao và tồn kho cách ly không khớp (bao: {storedBagWeight:0.###} kg, tồn: {inventoryWeight:0.###} kg).");

                decimal totalReleased = 0m;
                var touchedLocationIds = new HashSet<int>();
                foreach (var inv in inventories)
                {
                    var onHand = inv.QuantityOnHand;
                    if (onHand <= 0) continue;

                    totalReleased += onHand;

                    var txOut = new InventoryTransaction
                    {
                        InventoryId = inv.Id,
                        WarehouseId = inv.WarehouseId,
                        LocationId = inv.LocationId,
                        ProductVariantId = inv.ProductVariantId,
                        PaddyLotId = lot.Id,
                        TransactionType = InventoryTransactionTypeConstants.Export,
                        ReferenceType = InventoryReferenceTypeConstants.QualityInspection,
                        ReferenceId = entity.Id,
                        Quantity = -onHand,
                        BeforeQuantity = onHand,
                        AfterQuantity = 0,
                        WeightKg = onHand,
                        Note = $"Rút tồn khỏi ô cách ly sau khi kiểm tra lại đạt — lô {lot.LotCode}",
                        CreatedBy = obj.CreatedBy,
                        CreatedDate = now
                    };
                    await _inventoryTransactionRepository.CreateWithColumnTotalsAsync(txOut);

                    inv.QuantityOnHand = 0;
                    inv.LastModifiedDate = now;
                    inv.UpdatedBy = obj.CreatedBy;
                    await _inventoryRepository.UpdateAsync(inv);

                    if (inv.LocationId.HasValue) touchedLocationIds.Add(inv.LocationId.Value);
                }
                await _inventoryTransactionRepository.SaveChangesAsync();
                await _inventoryRepository.SaveChangesAsync();

                if (totalReleased <= 0)
                    return ApiResponse.BadRequest(message: "Lô cách ly không còn tồn kho để xếp lại.");

                // Put the physical bags back into the pending pool so the new inbound
                // line can place them in a normal location.
                foreach (var bag in storedBags)
                {
                    var fromLocationId = bag.LocationId;
                    touchedLocationIds.Add(fromLocationId!.Value);
                    bag.Status = PaddyLotBagStatuses.Pending;
                    bag.LocationId = null;
                    bag.StackOrder = 0;
                    bag.OpenBagKey = null;
                    bag.UpdatedBy = obj.CreatedBy;
                    bag.LastModifiedDate = now;

                    _context.PaddyLotBagMovements.Add(new PaddyLotBagMovement
                    {
                        BagId = bag.Id,
                        MovementType = PaddyLotBagMovementTypes.QualityRecheckRelease,
                        FromLocationId = fromLocationId,
                        ToLocationId = null,
                        WeightKg = bag.WeightKg,
                        BeforeWeightKg = bag.WeightKg,
                        AfterWeightKg = bag.WeightKg,
                        ReferenceType = InventoryReferenceTypeConstants.QualityInspection,
                        ReferenceId = entity.Id,
                        Note = $"Rút bao khỏi ô cách ly sau khi tái kiểm đạt — lô {lot.LotCode}",
                        CreatedBy = obj.CreatedBy,
                        CreatedDate = now
                    });
                }
                await _context.SaveChangesAsync();

                // 2. Đồng bộ lại sức chứa các ô cách ly vừa rút (self-healing = tổng tồn thực còn lại).
                foreach (var locId in touchedLocationIds)
                {
                    var loc = await _context.Locations.FirstOrDefaultAsync(x => x.Id == locId && !x.IsDeleted);
                    if (loc == null) continue;
                    loc.CurrentOccupancy = await _context.Inventories
                        .Where(x => x.LocationId == locId && !x.IsDeleted)
                        .SumAsync(x => x.QuantityOnHand);
                    if (loc.CurrentOccupancy <= 0.001m)
                    {
                        loc.CurrentOccupancy = 0m;
                        loc.CurrentProductVariantId = null;
                    }
                    loc.LastModifiedDate = now;
                    loc.UpdatedBy = obj.CreatedBy;
                }
                await _context.SaveChangesAsync();

                // 3. Đưa lô về CHỜ NHẬP KHO để xếp lại; QualityStatus = Passed để put-away gợi ý ô THƯỜNG.
                var pendingInboundStatus = await _lotStatusRepository.FirstOrDefaultAsync(x => x.Code == LotStatusCodeConstants.PendingInbound && !x.IsDeleted);
                lot.QualityStatus = QualityStatusConstants.Passed;
                lot.RemainingWeightKg = 0m; // tồn sẽ tăng lại khi xác nhận xếp kho
                lot.LocationId = null;
                if (pendingInboundStatus != null) lot.StatusId = pendingInboundStatus.Id;
                lot.LastModifiedDate = now;
                lot.UpdatedBy = obj.CreatedBy;
                await _paddyLotRepository.UpdateAsync(lot);
                await _paddyLotRepository.SaveChangesAsync();

                // 4. Sinh phiếu nhập kho (Draft) để đưa vào màn Store-in xếp lại vào ô thường.
                var lines = new List<InboundLine>
                {
                    new(lot.Id, lot.ProductVariantId, totalReleased, lot.CostPricePerKg)
                };
                await CreateReceiptInboundOrderAsync(lot, lines, obj.CreatedBy, now,
                    note: $"Xếp lại sau kiểm tra lại đạt — lô {lot.LotCode}");
            }
            else
            {
                // Vẫn cách ly — chỉ cập nhật dấu vết chất lượng.
                lot.QualityStatus = QualityStatusConstants.Failed;
                lot.LastModifiedDate = now;
                lot.UpdatedBy = obj.CreatedBy;
                await _paddyLotRepository.UpdateAsync(lot);
                await _paddyLotRepository.SaveChangesAsync();
            }

            await tx.CommitAsync();

            try
            {
                _scheduledJobService?.Enqueue<ILotQualityRecheckService>(s => s.EvaluateLotAsync(entity.PaddyLotId, CancellationToken.None));
            }
            catch
            {
                // Ignore to avoid disrupting business flow
            }
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return obj.PassedInspection
            ? ApiResponse.Created(entity.Id, "Kiểm tra lại đạt. Đã rút hàng khỏi ô cách ly và tạo phiếu nhập kho để xếp lại.")
            : ApiResponse.Created(entity.Id, "Đã ghi nhận kết quả kiểm tra lại. Lô vẫn ở trạng thái cách ly.");
    }

    /// <summary>Một dòng hàng của phiếu nhập kho sinh sau kiểm định.</summary>
    private readonly record struct InboundLine(int LotId, int ProductVariantId, decimal Weight, decimal UnitCost);

    /// <summary>
    /// Tạo phiếu nhập kho (InboundOrder) trạng thái Draft cho (các) lô sau khi kiểm định xong,
    /// liên kết với phiếu mua gốc của lô. Mỗi dòng = 1 InboundOrderItem để màn Nhập kho xử lý xếp
    /// vị trí (lô cách ly → gợi ý ô cách ly, lô đạt → ô lưu trữ thường).
    /// </summary>
    private async Task CreateReceiptInboundOrderAsync(PaddyLot receiptLot, List<InboundLine> lines, int? userId, DateTime now, string? note = null)
    {
        if (lines.Count == 0) return;

        // Quarantine children cannot copy SourceReceiptId because that relationship
        // is unique. Resolve the purchase receipt through the parent lot so the
        // re-entry order still retains farmer/source traceability.
        var sourceReceiptId = receiptLot.SourceReceiptId;
        if (!sourceReceiptId.HasValue && receiptLot.ParentLotId.HasValue)
        {
            sourceReceiptId = await _context.PaddyLots
                .Where(x => x.Id == receiptLot.ParentLotId.Value && !x.IsDeleted)
                .Select(x => x.SourceReceiptId)
                .FirstOrDefaultAsync();
        }

        var draftStatus = await _context.InboundOrderStatuses
            .FirstOrDefaultAsync(x => x.Code == InboundOrderStatusNames.Draft && !x.IsDeleted)
            ?? await _context.InboundOrderStatuses.FirstOrDefaultAsync(x => !x.IsDeleted)
            ?? throw new InvalidOperationException("Không tìm thấy InboundOrderStatus trong hệ thống.");

        var datePart = now.ToString("yyyyMMdd");
        var baseCode = $"INB-{datePart}";
        var cnt = await _context.InboundOrders.CountAsync(x => x.POCode != null && x.POCode.StartsWith(baseCode));
        var code = $"{baseCode}-{(cnt + 1):D4}";
        int attempts = 0;
        while (await _context.InboundOrders.AnyAsync(x => x.POCode == code && x.OrganizationId == receiptLot.OrganizationId) && attempts < 10)
        {
            attempts++;
            code = $"{baseCode}-{(cnt + 1 + attempts):D4}";
        }

        var order = new InboundOrder
        {
            WarehouseId = receiptLot.WarehouseId,
            InboundOrderStatusId = draftStatus.Id,
            POCode = code,
            PaddyPurchaseReceiptId = sourceReceiptId,
            OrganizationId = receiptLot.OrganizationId,
            SourceType = "RECEIPT",
            TotalAssetValue = lines.Sum(l => l.Weight * l.UnitCost),
            ExpectedDate = receiptLot.InboundDate,
            CompletedDate = null,
            Note = note ?? $"Nhập lúa sau kiểm định — lô {receiptLot.LotCode}",
            CreatedBy = userId,
            CreatedDate = now
        };
        _context.InboundOrders.Add(order);
        await _context.SaveChangesAsync();

        foreach (var l in lines)
        {
            _context.InboundOrderItems.Add(new InboundOrderItem
            {
                InboundOrderId = order.Id,
                ProductVariantId = l.ProductVariantId,
                PaddyLotId = l.LotId,
                QuantityOrdered = l.Weight,
                QuantityReceived = 0m,
                ExpectedWeightKg = l.Weight,
                ActualWeightKg = l.Weight,
                UnitCostPrice = l.UnitCost,
                CreatedBy = userId,
                CreatedDate = now
            });
        }
        await _context.SaveChangesAsync();
    }

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

        try
        {
            if (_scheduledJobService != null)
            {
                _scheduledJobService.Enqueue<ILotQualityRecheckService>(s => s.EvaluateLotAsync(entity.PaddyLotId, CancellationToken.None));
            }
        }
        catch
        {
            // Ignore
        }

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

        var lotIds = new List<int>();
        foreach (var id in objs)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity != null)
            {
                lotIds.Add(entity.PaddyLotId);
            }
        }

        var isDeleted = await _repo.SoftDeleteListAsync(objs);
        if (!isDeleted) return ApiResponse.BadRequest();
        await _repo.SaveChangesAsync();

        if (_scheduledJobService != null)
        {
            foreach (var lotId in lotIds.Distinct())
            {
                try
                {
                    _scheduledJobService.Enqueue<ILotQualityRecheckService>(s => s.EvaluateLotAsync(lotId, CancellationToken.None));
                }
                catch
                {
                    // Ignore
                }
            }
        }

        return ApiResponse.Success(isDeleted);
    }

    private async Task MoveSelectedBagsToQuarantineLotAsync(
        int parentLotId,
        int childLotId,
        IReadOnlyCollection<int> selectedBagIds,
        int inspectionId,
        int? userId,
        DateTime now)
    {
        if (selectedBagIds.Count == 0)
            throw new InvalidOperationException("Tách lô cách ly một phần bắt buộc phải có danh sách bao.");

        var bags = await _context.PaddyLotBags
            .Include(x => x.Contents)
            .Where(x => selectedBagIds.Contains(x.Id) && x.LotId == parentLotId && !x.IsDeleted)
            .ToListAsync();
        if (bags.Count != selectedBagIds.Count)
            throw new InvalidOperationException("Danh sách bao đã thay đổi trong lúc tách lô. Vui lòng tải lại và thử lại.");
        if (bags.Any(x => x.Contents
            .Any(c => !c.IsDeleted && c.WeightKg > 0 && c.LotId != parentLotId)))
            throw new InvalidOperationException("Không thể đổi lô đại diện của bao hỗn hợp khi tách cách ly một lô.");

        var contents = await _context.PaddyLotBagContents
            .Where(x => selectedBagIds.Contains(x.BagId) && x.LotId == parentLotId && !x.IsDeleted)
            .ToListAsync();

        foreach (var bag in bags)
        {
            bag.LotId = childLotId;
            bag.BagKind = PaddyLotBagKinds.Quarantine;
            bag.UpdatedBy = userId;
            bag.LastModifiedDate = now;
            _context.PaddyLotBagMovements.Add(new PaddyLotBagMovement
            {
                BagId = bag.Id,
                MovementType = PaddyLotBagMovementTypes.QualityQuarantineSplit,
                FromLocationId = bag.LocationId,
                ToLocationId = bag.LocationId,
                WeightKg = bag.WeightKg,
                BeforeWeightKg = bag.WeightKg,
                AfterWeightKg = bag.WeightKg,
                ReferenceType = InventoryReferenceTypeConstants.QualityInspectionSplit,
                ReferenceId = inspectionId,
                Note = $"Chuyển bao #{bag.BagNo} từ lô {parentLotId} sang lô cách ly {childLotId}",
                CreatedBy = userId,
                CreatedDate = now
            });
        }

        foreach (var content in contents)
        {
            content.LotId = childLotId;
            content.UpdatedBy = userId;
            content.LastModifiedDate = now;
        }

        await _context.SaveChangesAsync();
    }

    private static QualityInspectionDetailDto ToDto(QualityInspection x) => new()
    {
        Id = x.Id,
        PaddyLotId = x.PaddyLotId,
        LotCode = x.PaddyLot?.LotCode,
        LotStatusCode = x.PaddyLot?.Status?.Code,
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
