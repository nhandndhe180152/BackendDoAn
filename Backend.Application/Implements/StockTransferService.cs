using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Backend.Application.Constants;
using Backend.Application.DTOs.StockTransfers;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Nghiệp vụ phiếu chuyển kho nội bộ theo hai bước:
/// Nháp -> Đang chuyển (xuất kho nguồn) -> Hoàn tất (nhập kho đích).
/// </summary>
public class StockTransferService : IStockTransferService
{
    private readonly IStockTransferRepository _transferRepository;
    private readonly IRepositoryBase<StockTransferItem, int> _itemRepository;
    private readonly IRepositoryBase<StockTransferStatus, int> _statusRepository;
    private readonly IRepositoryBase<LotStatus, int> _lotStatusRepository;
    private readonly IRepositoryBase<Warehouse, int> _warehouseRepository;
    private readonly IRepositoryBase<ProductVariant, int> _productVariantRepository;
    private readonly IPaddyLotRepository _paddyLotRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryTransactionRepository _inventoryTransactionRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly IRepositoryBase<PaddyLotBag, int>? _bagRepository;
    private readonly IRepositoryBase<PaddyLotBagContent, int>? _bagContentRepository;
    private readonly IRepositoryBase<PaddyLotBagMovement, int>? _bagMovementRepository;
    private readonly IRepositoryBase<StockTransferBag, int>? _transferBagRepository;
    private readonly IRepositoryBase<InboundOrder, int>? _inboundOrderRepository;
    private readonly IRepositoryBase<InboundOrderItem, int>? _inboundOrderItemRepository;
    private readonly IRepositoryBase<InboundOrderStatus, int>? _inboundStatusRepository;

    public StockTransferService(
        IStockTransferRepository transferRepository,
        IRepositoryBase<StockTransferItem, int> itemRepository,
        IRepositoryBase<StockTransferStatus, int> statusRepository,
        IRepositoryBase<LotStatus, int> lotStatusRepository,
        IRepositoryBase<Warehouse, int> warehouseRepository,
        IRepositoryBase<ProductVariant, int> productVariantRepository,
        IPaddyLotRepository paddyLotRepository,
        IInventoryRepository inventoryRepository,
        IInventoryTransactionRepository inventoryTransactionRepository,
        ILocationRepository locationRepository,
        INotificationDispatcher notificationDispatcher,
        IRepositoryBase<PaddyLotBag, int>? bagRepository = null,
        IRepositoryBase<PaddyLotBagContent, int>? bagContentRepository = null,
        IRepositoryBase<PaddyLotBagMovement, int>? bagMovementRepository = null,
        IRepositoryBase<StockTransferBag, int>? transferBagRepository = null,
        IRepositoryBase<InboundOrder, int>? inboundOrderRepository = null,
        IRepositoryBase<InboundOrderItem, int>? inboundOrderItemRepository = null,
        IRepositoryBase<InboundOrderStatus, int>? inboundStatusRepository = null)
    {
        _transferRepository = transferRepository;
        _itemRepository = itemRepository;
        _statusRepository = statusRepository;
        _lotStatusRepository = lotStatusRepository;
        _warehouseRepository = warehouseRepository;
        _locationRepository = locationRepository;
        _productVariantRepository = productVariantRepository;
        _paddyLotRepository = paddyLotRepository;
        _inventoryRepository = inventoryRepository;
        _inventoryTransactionRepository = inventoryTransactionRepository;
        _notificationDispatcher = notificationDispatcher;
        _bagRepository = bagRepository;
        _bagContentRepository = bagContentRepository;
        _bagMovementRepository = bagMovementRepository;
        _transferBagRepository = transferBagRepository;
        _inboundOrderRepository = inboundOrderRepository;
        _inboundOrderItemRepository = inboundOrderItemRepository;
        _inboundStatusRepository = inboundStatusRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateStockTransferDto obj)
    {
        if (obj.FromWarehouseId == obj.ToWarehouseId)
            return ApiResponse.BadRequest(message: "Kho nguồn và kho đích không được trùng nhau.");

        var validationError = await ValidateTransferAsync(
            obj.FromWarehouseId,
            obj.ToWarehouseId,
            obj.Items,
            checkAvailableStock: true);
        if (validationError != null)
            return ApiResponse.UnprocessableEntity(validationError);

        var draftStatus = await GetStatusAsync(StockTransferStatusNames.Draft);
        if (draftStatus == null)
            return ApiResponse.UnprocessableEntity("Không tìm thấy trạng thái Nháp của phiếu chuyển kho.");

        var now = DateTimeHelper.VietnamNow();
        await using var transaction = await _transferRepository.BeginTransactionAsync();
        try
        {
            var transfer = new StockTransfer
            {
                OrganizationId = obj.OrganizationId,
                TransferCode = await GenerateTransferCodeAsync(now),
                StatusId = draftStatus.Id,
                FromWarehouseId = obj.FromWarehouseId,
                ToWarehouseId = obj.ToWarehouseId,
                TransferDate = obj.TransferDate == default ? now : obj.TransferDate,
                AssignedUserId = obj.AssignedUserId,
                Note = obj.Note?.Trim(),
                CreatedBy = obj.CreatedBy,
                CreatedDate = now
            };

            await _transferRepository.CreateAsync(transfer);
            await _transferRepository.SaveChangesAsync();

            var itemDtos = obj.Items.ToList();
            var items = itemDtos.Select(item => ToEntity(item, transfer.Id, obj.CreatedBy, now)).ToList();
            await _itemRepository.CreateListAsync(items);
            await _transferRepository.SaveChangesAsync();

            await PersistItemBagsAsync(itemDtos, items, obj.CreatedBy, now);
            await _transferRepository.EndTransactionAsync();

            return ApiResponse.Created(transfer.Id, "Tạo phiếu chuyển kho thành công.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await _transferRepository.RollbackTransactionAsync();
            return ApiResponse.Conflict("Dữ liệu tồn kho đã thay đổi. Vui lòng tải lại và thử lại.");
        }
        catch
        {
            await _transferRepository.RollbackTransactionAsync();
            throw;
        }
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateStockTransferDto> objs)
        => Task.FromResult(ApiResponse.Error(message: "CreateList chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> GetAllAsync()
    {
        // Chỉ đọc để map DTO → dùng no-tracking (theo quy ước repo: trackChanges=true ⇒ AsNoTracking).
        var entities = await _transferRepository
            .FindByCondition(x => !x.IsDeleted, true)
            .Include(x => x.Status)
            .Include(x => x.FromWarehouse)
            .Include(x => x.ToWarehouse)
            .Include(x => x.StockTransferItems.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.ProductVariant)
            .Include(x => x.StockTransferItems.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.PaddyLot)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        return ApiResponse.Success(entities.Select(ToDto).ToList());
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var entity = await TransferDetailQuery(id).FirstOrDefaultAsync();
        if (entity == null) return ApiResponse.NotFound();
        var dto = ToDto(entity);

        // Truy vết phiếu nhập kho tự sinh ở kho đích (nếu đã hoàn tất).
        if (_inboundOrderRepository != null)
        {
            var inbound = await _inboundOrderRepository
                .FindByCondition(x => x.StockTransferId == id && !x.IsDeleted)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();
            if (inbound != null)
            {
                dto.DestinationInboundOrderId = inbound.Id;
                dto.DestinationInboundOrderCode = inbound.POCode;
            }
        }

        return ApiResponse.Success(dto);
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        var data = await _transferRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query)
        => Task.FromResult(ApiResponse.Error(message: "GetPaged (SearchQuery) chưa được hỗ trợ.", status: 501));

    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
        => Task.FromResult(ApiResponse.Error(message: "GetPaged (AdvancedSearchQuery) chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> GetSummaryAsync()
    {
        var now = DateTimeHelper.VietnamNow();
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var nextMonth = monthStart.AddMonths(1);

        var query = _transferRepository
            .FindByCondition(x => !x.IsDeleted, false)
            .Include(x => x.Status)
            .Include(x => x.StockTransferItems);

        var transfersThisMonth = await query.CountAsync(x =>
            x.TransferDate >= monthStart &&
            x.TransferDate < nextMonth &&
            x.Status.Code != StockTransferStatusNames.Cancelled);

        var inTransitCount = await query.CountAsync(x =>
            x.Status.Code == StockTransferStatusNames.InTransit);

        var totalTransferredWeightKg = await query
            .Where(x =>
                x.TransferDate >= monthStart &&
                x.TransferDate < nextMonth &&
                (x.Status.Code == StockTransferStatusNames.InTransit ||
                 x.Status.Code == StockTransferStatusNames.Completed))
            .SelectMany(x => x.StockTransferItems.Where(i => !i.IsDeleted))
            .SumAsync(i => (decimal?)i.WeightKg) ?? 0m;

        return ApiResponse.Success(new StockTransferSummaryDto
        {
            TransfersThisMonth = transfersThisMonth,
            InTransitCount = inTransitCount,
            TotalTransferredWeightKg = totalTransferredWeightKg
        });
    }

    public async Task<ApiResponse> UpdateAsync(UpdateStockTransferDto obj)
    {
        var entity = await TransferDetailQuery(obj.Id).FirstOrDefaultAsync();
        if (entity == null) return ApiResponse.NotFound();
        if (!IsStatus(entity, StockTransferStatusNames.Draft))
            return ApiResponse.UnprocessableEntity("Chỉ phiếu ở trạng thái Nháp mới được chỉnh sửa.");

        var validationError = await ValidateTransferAsync(
            obj.FromWarehouseId,
            obj.ToWarehouseId,
            obj.Items,
            checkAvailableStock: true);
        if (validationError != null)
            return ApiResponse.UnprocessableEntity(validationError);

        var now = DateTimeHelper.VietnamNow();
        await using var transaction = await _transferRepository.BeginTransactionAsync();
        try
        {
            entity.OrganizationId = obj.OrganizationId;
            entity.FromWarehouseId = obj.FromWarehouseId;
            entity.ToWarehouseId = obj.ToWarehouseId;
            entity.TransferDate = obj.TransferDate;
            entity.AssignedUserId = obj.AssignedUserId;
            entity.Note = obj.Note?.Trim();
            entity.UpdatedBy = obj.UpdatedBy;
            entity.LastModifiedDate = now;
            await _transferRepository.UpdateAsync(entity);

            var currentItems = entity.StockTransferItems.Where(i => !i.IsDeleted).ToList();
            await SoftDeleteItemBagsAsync(currentItems.Select(i => i.Id), obj.UpdatedBy, now);
            foreach (var currentItem in currentItems)
            {
                currentItem.IsDeleted = true;
                currentItem.UpdatedBy = obj.UpdatedBy;
                currentItem.LastModifiedDate = now;
            }
            if (currentItems.Count > 0)
                await _itemRepository.UpdateListAsync(currentItems);

            var replacementDtos = obj.Items.ToList();
            var replacementItems = replacementDtos
                .Select(item => ToEntity(item, entity.Id, obj.UpdatedBy, now))
                .ToList();
            await _itemRepository.CreateListAsync(replacementItems);
            await _transferRepository.SaveChangesAsync();

            await PersistItemBagsAsync(replacementDtos, replacementItems, obj.UpdatedBy, now);
            await _transferRepository.EndTransactionAsync();
            return ApiResponse.Success(entity.Id, "Cập nhật phiếu chuyển kho thành công.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await _transferRepository.RollbackTransactionAsync();
            return ApiResponse.Conflict("Dữ liệu đã thay đổi. Vui lòng tải lại và thử lại.");
        }
        catch
        {
            await _transferRepository.RollbackTransactionAsync();
            throw;
        }
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateStockTransferDto> objs)
        => Task.FromResult(ApiResponse.Error(message: "UpdateList chưa được hỗ trợ.", status: 501));

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var entity = await _transferRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted, false, x => x.Status)
            .FirstOrDefaultAsync();
        if (entity == null) return ApiResponse.NotFound();
        if (!IsStatus(entity, StockTransferStatusNames.Draft))
            return ApiResponse.UnprocessableEntity("Chỉ phiếu Nháp mới có thể xóa.");

        var isDeleted = await _transferRepository.SoftDeleteAsync(id);
        if (!isDeleted) return ApiResponse.BadRequest();
        await _transferRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        var isDeleted = await _transferRepository.SoftDeleteListAsync(objs);
        if (!isDeleted) return ApiResponse.BadRequest();
        await _transferRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public async Task<ApiResponse> DispatchAsync(int id, int dispatchedById)
    {
        var transfer = await TransferDetailQuery(id).FirstOrDefaultAsync();
        if (transfer == null) return ApiResponse.NotFound();
        if (!IsStatus(transfer, StockTransferStatusNames.Draft))
            return ApiResponse.UnprocessableEntity("Chỉ phiếu Nháp mới được xuất chuyển.");

        var activeItems = transfer.StockTransferItems.Where(i => !i.IsDeleted).ToList();
        var itemDtos = activeItems.Select(ToItemDto).ToList();
        // Nạp bao đã lưu (StockTransferBag) vào DTO để verify lại chất lượng/LIFO/tồn trước khi xuất.
        for (var i = 0; i < activeItems.Count; i++)
            itemDtos[i].Bags = await LoadItemBagInputsAsync(activeItems[i].Id);
        var validationError = await ValidateTransferAsync(
            transfer.FromWarehouseId,
            transfer.ToWarehouseId,
            itemDtos,
            checkAvailableStock: true);
        if (validationError != null)
            return ApiResponse.UnprocessableEntity(validationError);

        // Phiếu cũ có thể chưa lưu FromLocationId. Khi lô đã có vị trí nguồn,
        // lấy vị trí từ PaddyLot và lưu lại trước khi xuất chuyển.
        for (var index = 0; index < activeItems.Count; index++)
            activeItems[index].FromLocationId = itemDtos[index].FromLocationId;

        var inTransitStatus = await GetStatusAsync(StockTransferStatusNames.InTransit);
        if (inTransitStatus == null)
            return ApiResponse.UnprocessableEntity("Không tìm thấy trạng thái Đang chuyển.");

        var now = DateTimeHelper.VietnamNow();
        await using var transaction = await _transferRepository.BeginTransactionAsync();
        try
        {
            foreach (var item in activeItems)
            {
                var duplicated = await _inventoryTransactionRepository.AnyAsync(x =>
                    x.ReferenceType == InventoryReferenceTypeConstants.StockTransfer &&
                    x.ReferenceId == transfer.Id &&
                    x.ReferenceItemId == item.Id &&
                    x.TransactionType == InventoryTransactionTypeConstants.Export);
                if (duplicated)
                    throw new InvalidOperationException($"Dòng hàng {item.Id} đã được xuất chuyển trước đó.");

                var bagRows = _transferBagRepository != null
                    ? await _transferBagRepository
                        .FindByCondition(x => x.StockTransferItemId == item.Id && !x.IsDeleted, false)
                        .ToListAsync()
                    : new List<StockTransferBag>();

                if (bagRows.Count > 0)
                {
                    // ── Luồng THEO BAO: verify chất lượng đã nhập → chuyển đi / cách ly / bỏ ──
                    await DispatchItemBagsAsync(transfer, item, bagRows, dispatchedById, now);
                }
                else
                {
                    // ── Fallback theo KG cho tồn cũ chưa có bao ──
                    decimal costPrice;
                    if (item.PaddyLotId.HasValue)
                    {
                        var lot = await _paddyLotRepository.GetByIdAsync(item.PaddyLotId.Value)
                            ?? throw new InvalidOperationException($"Không tìm thấy lô ID {item.PaddyLotId.Value}.");
                        costPrice = lot.CostPricePerKg;
                    }
                    else
                    {
                        var sourceInventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                            item.ProductVariantId, transfer.FromWarehouseId, item.FromLocationId);
                        costPrice = sourceInventory?.CostPrice ?? 0m;
                    }

                    await MoveInventoryAsync(
                        item.ProductVariantId, transfer.FromWarehouseId, item.FromLocationId,
                        item.WeightKg, isExport: true, costPrice,
                        transfer.Id, item.Id, dispatchedById, now,
                        $"Xuất chuyển từ kho {transfer.FromWarehouseId} đến kho {transfer.ToWarehouseId}",
                        item.PaddyLotId);
                }
            }

            transfer.StatusId = inTransitStatus.Id;
            transfer.UpdatedBy = dispatchedById;
            transfer.LastModifiedDate = now;
            await _transferRepository.UpdateAsync(transfer);
            await _transferRepository.SaveChangesAsync();
            await _transferRepository.EndTransactionAsync();

            return ApiResponse.Success(
                new { TransferId = id },
                $"Phiếu chuyển {transfer.TransferCode} đã xuất khỏi kho nguồn.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await _transferRepository.RollbackTransactionAsync();
            return ApiResponse.Conflict("Tồn kho nguồn đã thay đổi. Vui lòng tải lại và kiểm tra.");
        }
        catch (InvalidOperationException ex)
        {
            await _transferRepository.RollbackTransactionAsync();
            return ApiResponse.UnprocessableEntity(ex.Message);
        }
        catch (DbUpdateException ex)
        {
            await _transferRepository.RollbackTransactionAsync();
            var detail = ex.InnerException?.Message ?? ex.Message;
            return ApiResponse.UnprocessableEntity($"Không thể xuất chuyển do lỗi dữ liệu: {detail}");
        }
        catch
        {
            await _transferRepository.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<ApiResponse> ReceiveAsync(int id, int receivedById)
    {
        var transfer = await TransferDetailQuery(id).FirstOrDefaultAsync();
        if (transfer == null) return ApiResponse.NotFound();
        if (!IsStatus(transfer, StockTransferStatusNames.InTransit))
            return ApiResponse.UnprocessableEntity("Chỉ phiếu Đang chuyển mới được xác nhận nhận hàng.");

        var completedStatus = await GetStatusAsync(StockTransferStatusNames.Completed);
        if (completedStatus == null)
            return ApiResponse.UnprocessableEntity("Không tìm thấy trạng thái Hoàn tất.");

        var activeItems = transfer.StockTransferItems.Where(i => !i.IsDeleted).ToList();
        var now = DateTimeHelper.VietnamNow();
        await using var transaction = await _transferRepository.BeginTransactionAsync();
        try
        {
            // Gom các dòng lô mới sinh ở kho đích để tạo MỘT phiếu nhập kho truy vết.
            var inboundLines = new List<DestinationInboundLine>();

            foreach (var item in activeItems)
            {
                var duplicated = await _inventoryTransactionRepository.AnyAsync(x =>
                    x.ReferenceType == InventoryReferenceTypeConstants.StockTransfer &&
                    x.ReferenceId == transfer.Id &&
                    x.ReferenceItemId == item.Id &&
                    x.TransactionType == InventoryTransactionTypeConstants.Import &&
                    x.WarehouseId == transfer.ToWarehouseId);
                if (duplicated)
                    throw new InvalidOperationException($"Dòng hàng {item.Id} đã được nhận trước đó.");

                // Giữ vị trí đích nếu còn hợp lệ; nếu chưa chọn / đã bị chiếm bởi SKU khác / đầy
                // thì tự chọn lại ô hợp lệ → tránh lỗi "Vị trí đích đang chứa một loại sản phẩm khác".
                var resolvedTo = await EnsureUsableDestinationAsync(
                    item.ToLocationId, transfer.ToWarehouseId, item.ProductVariantId, item.WeightKg);
                if (resolvedTo != item.ToLocationId)
                {
                    item.ToLocationId = resolvedTo;
                    await _itemRepository.UpdateAsync(item);
                }

                await ValidateDestinationLocationAsync(
                    item.ToLocationId,
                    transfer.ToWarehouseId,
                    item.ProductVariantId,
                    item.WeightKg);

                var allBagRows = _transferBagRepository != null
                    ? await _transferBagRepository
                        .FindByCondition(x => x.StockTransferItemId == item.Id && !x.IsDeleted, false)
                        .ToListAsync()
                    : new List<StockTransferBag>();
                var transferBagRows = allBagRows
                    .Where(x => x.Disposition == StockTransferBagDispositions.Transfer)
                    .ToList();

                if (allBagRows.Count > 0)
                {
                    // ── Luồng THEO BAO: chỉ nhận bao ĐẠT (đang chuyển). Bao cách ly/bỏ đã xử lý ở nguồn. ──
                    if (transferBagRows.Count > 0)
                    {
                        await ReceiveItemBagsAsync(transfer, item, transferBagRows, receivedById, now, inboundLines);
                        // Chốt thay đổi bao ngay để dòng sau còn "thấy" bao lẻ vừa đặt → không chọn trùng cột.
                        await _transferRepository.SaveChangesAsync();
                    }
                    continue;
                }

                // ── Fallback theo KG cho tồn cũ chưa có bao ──
                var targetLotId = item.PaddyLotId;
                decimal costPrice;
                if (item.PaddyLotId.HasValue)
                {
                    var lot = await _paddyLotRepository.GetByIdAsync(item.PaddyLotId.Value)
                        ?? throw new InvalidOperationException($"Không tìm thấy lô ID {item.PaddyLotId.Value}.");
                    if (lot.ProductVariantId != item.ProductVariantId)
                        throw new InvalidOperationException($"Sản phẩm của lô {lot.LotCode} không khớp dòng chuyển kho.");
                    if (item.WeightKg > lot.RemainingWeightKg)
                        throw new InvalidOperationException($"Khối lượng nhận vượt khối lượng còn lại của lô {lot.LotCode}.");

                    item.FromLocationId ??= lot.LocationId;
                    costPrice = lot.CostPricePerKg;
                    targetLotId = await MoveLotToDestinationAsync(
                        lot, item.WeightKg, transfer.ToWarehouseId, item.ToLocationId, receivedById, now);
                }
                else
                {
                    var sourceInventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                        item.ProductVariantId, transfer.FromWarehouseId, item.FromLocationId);
                    costPrice = sourceInventory?.CostPrice ?? 0m;
                }

                await MoveInventoryAsync(
                    item.ProductVariantId, transfer.ToWarehouseId, item.ToLocationId,
                    item.WeightKg, isExport: false, costPrice,
                    transfer.Id, item.Id, receivedById, now,
                    $"Nhận hàng chuyển từ kho {transfer.FromWarehouseId}", targetLotId);

                if (item.WeightKg > 0)
                    inboundLines.Add(new DestinationInboundLine(item.ProductVariantId, targetLotId, item.WeightKg, costPrice));
            }

            transfer.StatusId = completedStatus.Id;
            transfer.UpdatedBy = receivedById;
            transfer.LastModifiedDate = now;
            await _transferRepository.UpdateAsync(transfer);
            await _transferRepository.SaveChangesAsync();
            await _transferRepository.EndTransactionAsync();

            // Phiếu nhập kho ở kho đích chỉ là chứng từ TRUY VẾT — tạo SAU khi đã chốt nhận hàng và
            // không được làm hỏng kết quả nhận hàng nếu gặp lỗi. Tạo ngoài transaction chính.
            try
            {
                await CreateDestinationInboundOrderAsync(transfer, inboundLines, receivedById, now);
            }
            catch
            {
                // Tồn kho/lô/bao đã commit; lỗi tạo phiếu nhập truy vết không làm sai kết quả nhận hàng.
            }

            try
            {
                await _notificationDispatcher.DispatchAsync(
                    NotificationConstants.Code.StockTransferConfirmed,
                    new NotificationTarget
                    {
                        RoleIds = new List<int>
                        {
                            CommonConstants.Role.WAREHOUSE,
                            CommonConstants.Role.OWNER
                        }
                    },
                    new object[] { transfer.TransferCode },
                    "/admin/stock-transfers",
                    receivedById);
            }
            catch
            {
                // Tồn kho đã được commit; lỗi gửi thông báo không được làm sai kết quả nhận hàng.
            }

            return ApiResponse.Success(
                new { TransferId = id },
                $"Phiếu chuyển {transfer.TransferCode} đã được nhận. Tồn kho đích đã tăng.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await _transferRepository.RollbackTransactionAsync();
            return ApiResponse.Conflict("Tồn kho đích đã thay đổi. Vui lòng tải lại và kiểm tra.");
        }
        catch (InvalidOperationException ex)
        {
            await _transferRepository.RollbackTransactionAsync();
            return ApiResponse.UnprocessableEntity(ex.Message);
        }
        catch (DbUpdateException ex)
        {
            await _transferRepository.RollbackTransactionAsync();
            var detail = ex.InnerException?.Message ?? ex.Message;
            return ApiResponse.UnprocessableEntity($"Không thể nhận hàng do lỗi dữ liệu ở kho đích: {detail}");
        }
        catch
        {
            await _transferRepository.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<ApiResponse> CancelAsync(int id, string? reason, int cancelledById)
    {
        var transfer = await _transferRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted, false, x => x.Status)
            .FirstOrDefaultAsync();
        if (transfer == null) return ApiResponse.NotFound();
        if (!IsStatus(transfer, StockTransferStatusNames.Draft))
            return ApiResponse.UnprocessableEntity("Chỉ phiếu Nháp mới được hủy.");

        var cancelledStatus = await GetStatusAsync(StockTransferStatusNames.Cancelled);
        if (cancelledStatus == null)
            return ApiResponse.UnprocessableEntity("Không tìm thấy trạng thái Hủy.");

        transfer.StatusId = cancelledStatus.Id;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            var reasonText = $"Lý do hủy: {reason.Trim()}";
            transfer.Note = string.IsNullOrWhiteSpace(transfer.Note)
                ? reasonText
                : $"{transfer.Note}\n{reasonText}";
        }
        transfer.UpdatedBy = cancelledById;
        transfer.LastModifiedDate = DateTimeHelper.VietnamNow();
        await _transferRepository.UpdateAsync(transfer);
        await _transferRepository.SaveChangesAsync();

        return ApiResponse.Success(new { TransferId = id }, "Hủy phiếu chuyển kho thành công.");
    }

    private IQueryable<StockTransfer> TransferDetailQuery(int id)
    {
        return _transferRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted, false)
            .Include(x => x.Status)
            .Include(x => x.FromWarehouse)
            .Include(x => x.ToWarehouse)
            .Include(x => x.AssignedUser)
            .Include(x => x.StockTransferItems.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.ProductVariant)
            .Include(x => x.StockTransferItems.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.PaddyLot)
                    .ThenInclude(lot => lot!.Location)
            .Include(x => x.StockTransferItems.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.FromLocation)
            .Include(x => x.StockTransferItems.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.ToLocation)
            .Include(x => x.StockTransferItems.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Bags.Where(b => !b.IsDeleted))
                    .ThenInclude(b => b.Bag)
            .Include(x => x.StockTransferItems.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Bags.Where(b => !b.IsDeleted))
                    .ThenInclude(b => b.SourceLot)
            .Include(x => x.StockTransferItems.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Bags.Where(b => !b.IsDeleted))
                    .ThenInclude(b => b.TargetLot)
            .Include(x => x.StockTransferItems.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Bags.Where(b => !b.IsDeleted))
                    .ThenInclude(b => b.QuarantineLocation);
    }

    private async Task<string?> ValidateTransferAsync(
        int fromWarehouseId,
        int toWarehouseId,
        IReadOnlyCollection<StockTransferItemDto>? items,
        bool checkAvailableStock)
    {
        if (fromWarehouseId == toWarehouseId)
            return "Kho nguồn và kho đích không được trùng nhau.";
        if (items == null || items.Count == 0)
            return "Phiếu chuyển kho phải có ít nhất một dòng hàng.";

        var fromWarehouse = await _warehouseRepository.GetByIdAsync(fromWarehouseId);
        var toWarehouse = await _warehouseRepository.GetByIdAsync(toWarehouseId);
        if (fromWarehouse == null || !fromWarehouse.IsActive)
            return "Kho nguồn không tồn tại hoặc đã ngưng hoạt động.";
        if (toWarehouse == null || !toWarehouse.IsActive)
            return "Kho đích không tồn tại hoặc đã ngưng hoạt động.";

        var duplicatedLine = items
            .GroupBy(x => new { x.ProductVariantId, x.PaddyLotId, x.FromLocationId })
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicatedLine != null)
            return "Không được thêm trùng sản phẩm, lô và vị trí nguồn trong cùng phiếu.";

        foreach (var item in items)
        {
            // ── Luồng MỚI: chuyển kho THEO BAO kèm kiểm định chất lượng + cách xử lý ──
            if (item.Bags.Count > 0)
            {
                var bagError = await ValidateItemBagsAsync(item, fromWarehouseId, checkAvailableStock);
                if (bagError != null)
                    return bagError;
            }
            else if (item.BagIds.Count > 0)
            {
                if (_bagRepository == null)
                    return "Dịch vụ quản lý bao chưa được cấu hình.";
                if (item.BagIds.Distinct().Count() != item.BagIds.Count)
                    return "Danh sách bao chuyển kho không được trùng lặp.";
                var selectedBags = await _bagRepository.FindByCondition(x => item.BagIds.Contains(x.Id) && !x.IsDeleted)
                    .Include(x => x.Contents).ThenInclude(x => x.Lot).ToListAsync();
                if (selectedBags.Count != item.BagIds.Count)
                    return "Có bao chuyển kho không tồn tại.";
                if (selectedBags.Any(x => x.Status != PaddyLotBagStatuses.Stored || x.LocationId != item.FromLocationId))
                    return "Tất cả bao phải đang lưu tại đúng vị trí nguồn.";
                if (selectedBags.Any(x => Math.Abs(x.WeightKg - x.Contents.Where(c => !c.IsDeleted && c.WeightKg > 0).Sum(c => c.WeightKg)) > 0.001m))
                    return "Có bao có tổng khối lượng không khớp thành phần lô. Vui lòng đối soát bao trước khi chuyển.";
                if (selectedBags.SelectMany(x => x.Contents).Any(x => !x.IsDeleted && x.WeightKg > 0 && x.Lot.ProductVariantId != item.ProductVariantId))
                    return "Bao hỗn hợp có thành phần không cùng SKU với dòng chuyển kho.";
                var selectedWeight = selectedBags.Sum(x => x.WeightKg);
                if (Math.Abs(selectedWeight - item.WeightKg) > 0.001m)
                    item.WeightKg = selectedWeight;
                if (checkAvailableStock)
                {
                    foreach (var contentGroup in selectedBags.SelectMany(x => x.Contents)
                                 .Where(x => !x.IsDeleted && x.WeightKg > 0)
                                 .GroupBy(x => x.LotId))
                    {
                        var inventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                            item.ProductVariantId, fromWarehouseId, item.FromLocationId, contentGroup.Key);
                        var available = (inventory?.QuantityOnHand ?? 0) - (inventory?.QuantityReserved ?? 0);
                        if (available + 0.001m < contentGroup.Sum(x => x.WeightKg))
                            return $"Tồn khả dụng của lô {contentGroup.First().Lot.LotCode} không đủ cho thành phần trong các bao đã chọn.";
                    }
                }
                if (item.FromLocationId.HasValue)
                {
                    var topIds = await _bagRepository.FindByCondition(x => x.LocationId == item.FromLocationId && x.Status == PaddyLotBagStatuses.Stored && !x.IsDeleted)
                        .OrderByDescending(x => x.StackOrder).ThenByDescending(x => x.Id)
                        .Take(selectedBags.Count).Select(x => x.Id).ToListAsync();
                    if (topIds.Except(item.BagIds).Any() || item.BagIds.Except(topIds).Any())
                        return "Chỉ được chuyển các bao trên cùng của chồng (LIFO).";
                }
            }
            if (item.Bags.Count == 0 && item.WeightKg <= 0)
                return "Khối lượng chuyển phải lớn hơn 0.";

            var productVariant = await _productVariantRepository.GetByIdAsync(item.ProductVariantId);
            if (productVariant == null || !productVariant.IsActive)
                return $"Sản phẩm ID {item.ProductVariantId} không tồn tại hoặc đã ngưng hoạt động.";

            if (item.FromLocationId.HasValue)
            {
                var sourceLocation = await _locationRepository.GetByIdAsync(item.FromLocationId.Value);
                if (sourceLocation == null || !sourceLocation.IsActive)
                    return $"Vị trí nguồn ID {item.FromLocationId.Value} không tồn tại hoặc đã ngưng hoạt động.";
                if (sourceLocation.WarehouseId != fromWarehouseId)
                    return "Vị trí nguồn không thuộc kho nguồn.";
                if (sourceLocation.IsOutboundStaging)
                    return "Không thể dùng khu chờ xuất làm vị trí nguồn của phiếu chuyển kho.";
                if (sourceLocation.OutboundLockOrderId.HasValue)
                    return $"Vị trí nguồn đang được phiếu xuất #{sourceLocation.OutboundLockOrderId} khóa.";
                if (sourceLocation.IsQuarantine)
                    return "Không thể chuyển hàng đang ở vị trí cách ly.";
            }

            if (item.ToLocationId.HasValue)
            {
                var destinationLocation = await _locationRepository.GetByIdAsync(item.ToLocationId.Value);
                if (destinationLocation == null || !destinationLocation.IsActive)
                    return $"Vị trí đích ID {item.ToLocationId.Value} không tồn tại hoặc đã ngưng hoạt động.";
                if (destinationLocation.WarehouseId != toWarehouseId)
                    return "Vị trí đích không thuộc kho đích.";
                if (destinationLocation.IsOutboundStaging)
                    return "Không thể dùng khu chờ xuất làm vị trí đích của phiếu chuyển kho.";
                if (destinationLocation.OutboundLockOrderId.HasValue)
                    return $"Vị trí đích đang được phiếu xuất #{destinationLocation.OutboundLockOrderId} khóa.";
                if (destinationLocation.IsQuarantine)
                    return "Không thể chọn vị trí cách ly làm vị trí nhận hàng.";

                if (_bagRepository != null)
                {
                    var destinationHasOpenBag = await _bagRepository.AnyAsync(x =>
                        x.LocationId == item.ToLocationId.Value && x.Status == PaddyLotBagStatuses.Stored &&
                        !x.IsFull && x.WeightKg > 0 && !x.IsDeleted);
                    if (destinationHasOpenBag)
                        return "Cột đích đang có bao mở nên không thể nhận thêm bao. Hãy chọn một cột trống/hàng lẻ khác.";

                    if (item.BagIds.Count > 0)
                    {
                        var incomingHasOpenBag = await _bagRepository.AnyAsync(x =>
                            item.BagIds.Contains(x.Id) && !x.IsFull && x.WeightKg > 0 && !x.IsDeleted);
                        var destinationHasStoredBag = await _bagRepository.AnyAsync(x =>
                            x.LocationId == item.ToLocationId.Value && x.Status == PaddyLotBagStatuses.Stored &&
                            x.WeightKg > 0 && !x.IsDeleted);
                        if (incomingHasOpenBag && destinationHasStoredBag)
                            return "Bao mở chỉ được chuyển vào cột trống dành cho hàng lẻ; không được đặt lên một chồng bao đang có hàng.";
                    }
                }
            }

            if (item.PaddyLotId.HasValue && item.BagIds.Count == 0 && item.Bags.Count == 0)
            {
                var lot = await _paddyLotRepository.GetByIdAsync(item.PaddyLotId.Value);
                if (lot == null)
                    return $"Không tìm thấy lô ID {item.PaddyLotId.Value}.";
                if (lot.WarehouseId != fromWarehouseId)
                    return $"Lô {lot.LotCode} không thuộc kho nguồn.";

                // Dữ liệu nhập kho cũ có thể chỉ gắn vị trí trên PaddyLot mà
                // StockTransferItem chưa có FromLocationId.
                item.FromLocationId ??= lot.LocationId;
                if (item.FromLocationId.HasValue &&
                    lot.LocationId != item.FromLocationId)
                    return $"Vị trí nguồn của lô {lot.LotCode} không còn khớp với phiếu chuyển.";
                if (lot.ProductVariantId != item.ProductVariantId)
                    return $"Sản phẩm của lô {lot.LotCode} không khớp dòng chuyển kho.";
                if (await IsLotBlockedForTransferAsync(lot.StatusId))
                    return $"Lô {lot.LotCode} đang bị cách ly và không thể chuyển kho.";
                if (item.WeightKg > lot.RemainingWeightKg)
                    return $"Khối lượng chuyển vượt phần còn lại của lô {lot.LotCode}.";
            }

            if (checkAvailableStock && item.BagIds.Count == 0 && item.Bags.Count == 0)
            {
                var inventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                    item.ProductVariantId,
                    fromWarehouseId,
                    item.FromLocationId,
                    item.PaddyLotId);
                if (inventory == null)
                    return "Không tìm thấy dòng tồn kho nguồn tương ứng.";

                var available = inventory.QuantityOnHand - inventory.QuantityReserved;
                if (available < item.WeightKg)
                    return $"Tồn khả dụng không đủ. Khả dụng {available} kg, cần chuyển {item.WeightKg} kg.";
            }
        }

        return null;
    }

    /// <summary>
    /// Kiểm tra danh sách bao (luồng THEO BAO) của một dòng chuyển kho: tồn tại, đúng cột nguồn,
    /// cùng SKU, LIFO đỉnh cột, tồn khả dụng; và kiểm định chất lượng ↔ cách xử lý hợp lệ.
    /// Đồng thời gán <c>WeightKg</c> của dòng = tổng khối lượng các bao ĐẠT chất lượng (sẽ chuyển đi).
    /// </summary>
    private async Task<string?> ValidateItemBagsAsync(
        StockTransferItemDto item,
        int fromWarehouseId,
        bool checkAvailableStock)
    {
        if (_bagRepository == null || _transferBagRepository == null)
            return "Dịch vụ quản lý bao chưa được cấu hình.";

        var bagIds = item.Bags.Select(b => b.BagId).ToList();
        if (bagIds.Distinct().Count() != bagIds.Count)
            return "Danh sách bao chuyển kho không được trùng lặp.";

        var selectedBags = await _bagRepository.FindByCondition(x => bagIds.Contains(x.Id) && !x.IsDeleted)
            .Include(x => x.Contents).ThenInclude(x => x.Lot).ToListAsync();
        if (selectedBags.Count != bagIds.Count)
            return "Có bao chuyển kho không tồn tại.";
        if (selectedBags.Any(x => x.Status != PaddyLotBagStatuses.Stored || x.LocationId != item.FromLocationId))
            return "Tất cả bao phải đang lưu tại đúng vị trí nguồn.";
        if (selectedBags.Any(x => Math.Abs(x.WeightKg - x.Contents.Where(c => !c.IsDeleted && c.WeightKg > 0).Sum(c => c.WeightKg)) > 0.001m))
            return "Có bao có tổng khối lượng không khớp thành phần lô. Vui lòng đối soát bao trước khi chuyển.";
        if (selectedBags.SelectMany(x => x.Contents).Any(x => !x.IsDeleted && x.WeightKg > 0 && x.Lot.ProductVariantId != item.ProductVariantId))
            return "Bao hỗn hợp có thành phần không cùng SKU với dòng chuyển kho.";

        // Tồn khả dụng: mọi bao được chọn (đạt hay không) đều RỜI cột nguồn.
        if (checkAvailableStock)
        {
            foreach (var contentGroup in selectedBags.SelectMany(x => x.Contents)
                         .Where(x => !x.IsDeleted && x.WeightKg > 0)
                         .GroupBy(x => x.LotId))
            {
                var inventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                    item.ProductVariantId, fromWarehouseId, item.FromLocationId, contentGroup.Key);
                var available = (inventory?.QuantityOnHand ?? 0) - (inventory?.QuantityReserved ?? 0);
                if (available + 0.001m < contentGroup.Sum(x => x.WeightKg))
                    return $"Tồn khả dụng của lô {contentGroup.First().Lot.LotCode} không đủ cho thành phần trong các bao đã chọn.";
            }
        }

        // LIFO: chỉ được lấy các bao trên cùng của chồng ở cột nguồn.
        if (item.FromLocationId.HasValue)
        {
            var topIds = await _bagRepository.FindByCondition(x => x.LocationId == item.FromLocationId && x.Status == PaddyLotBagStatuses.Stored && !x.IsDeleted)
                .OrderByDescending(x => x.StackOrder).ThenByDescending(x => x.Id)
                .Take(selectedBags.Count).Select(x => x.Id).ToListAsync();
            if (topIds.Except(bagIds).Any() || bagIds.Except(topIds).Any())
                return "Chỉ được chuyển các bao trên cùng của chồng (LIFO).";
        }

        // Kiểm định chất lượng ↔ cách xử lý từng bao.
        var weightByBag = selectedBags.ToDictionary(x => x.Id, x => x.WeightKg);
        foreach (var bag in item.Bags)
        {
            var quality = string.IsNullOrWhiteSpace(bag.QualityResult) ? BagQualityResultConstants.Pass : bag.QualityResult;
            if (quality != BagQualityResultConstants.Pass && quality != BagQualityResultConstants.IssueDetected)
                return "Kết quả kiểm định bao chỉ nhận PASS hoặc ISSUE_DETECTED.";
            bag.QualityResult = quality;

            if (string.IsNullOrWhiteSpace(bag.Disposition))
                bag.Disposition = quality == BagQualityResultConstants.Pass
                    ? StockTransferBagDispositions.Transfer
                    : StockTransferBagDispositions.Quarantine;
            if (!StockTransferBagDispositions.IsValid(bag.Disposition))
                return "Cách xử lý bao không hợp lệ (TRANSFER | QUARANTINE | DISPOSE).";

            if (quality == BagQualityResultConstants.Pass && bag.Disposition != StockTransferBagDispositions.Transfer)
                return "Bao ĐẠT chất lượng phải được chuyển đi (Disposition = TRANSFER).";
            if (quality == BagQualityResultConstants.IssueDetected && bag.Disposition == StockTransferBagDispositions.Transfer)
                return "Bao KHÔNG đạt chất lượng phải chọn cách ly hoặc bỏ bao.";

            if (bag.Disposition == StockTransferBagDispositions.Quarantine && bag.QuarantineLocationId.HasValue)
            {
                var qLoc = await _locationRepository.GetByIdAsync(bag.QuarantineLocationId.Value);
                if (qLoc == null || !qLoc.IsActive || qLoc.WarehouseId != fromWarehouseId || !qLoc.IsQuarantine)
                    return "Ô cách ly đã chọn không hợp lệ hoặc không thuộc khu cách ly của kho nguồn.";
            }
        }

        // Khối lượng dòng = tổng bao ĐẠT (sẽ thực sự chuyển sang kho đích).
        item.WeightKg = item.Bags
            .Where(b => b.Disposition == StockTransferBagDispositions.Transfer)
            .Sum(b => weightByBag.TryGetValue(b.BagId, out var w) ? w : 0m);

        return null;
    }

    /// <summary>Nạp bao đã lưu của một dòng chuyển kho thành DTO đầu vào (để verify lại khi xuất).</summary>
    private async Task<List<StockTransferBagInputDto>> LoadItemBagInputsAsync(int stockTransferItemId)
    {
        if (_transferBagRepository == null) return new List<StockTransferBagInputDto>();
        var rows = await _transferBagRepository
            .FindByCondition(x => x.StockTransferItemId == stockTransferItemId && !x.IsDeleted)
            .ToListAsync();
        return rows.Select(r => new StockTransferBagInputDto
        {
            BagId = r.BagId,
            MoisturePercent = r.MoisturePercent,
            ImpurityPercent = r.ImpurityPercent,
            MoldLevel = r.MoldLevel,
            PestLevel = r.PestLevel,
            PackagingStatus = r.PackagingStatus,
            QualityResult = r.QualityResult,
            Disposition = r.Disposition,
            QuarantineLocationId = r.QuarantineLocationId,
            QualityNote = r.QualityNote,
            Note = r.Note
        }).ToList();
    }

    /// <summary>Lưu các bao (StockTransferBag) cho từng dòng chuyển kho sau khi dòng đã có Id.</summary>
    private async Task PersistItemBagsAsync(
        IReadOnlyList<StockTransferItemDto> dtos,
        IReadOnlyList<StockTransferItem> entities,
        int? userId,
        DateTime now)
    {
        if (_transferBagRepository == null) return;

        var rows = new List<StockTransferBag>();
        for (var i = 0; i < entities.Count; i++)
        {
            var dto = dtos[i];
            if (dto.Bags.Count == 0) continue;

            // Lô nguồn đại diện + khối lượng thực tế của mỗi bao (để truy vết + hiển thị ngay).
            var bagIds = dto.Bags.Select(b => b.BagId).ToList();
            var sourceLotByBag = new Dictionary<int, int?>();
            var weightByBag = new Dictionary<int, decimal>();
            if (_bagRepository != null)
            {
                var bags = await _bagRepository.FindByCondition(x => bagIds.Contains(x.Id) && !x.IsDeleted)
                    .Include(x => x.Contents).ToListAsync();
                foreach (var bag in bags)
                {
                    var main = bag.Contents.Where(c => !c.IsDeleted && c.WeightKg > 0)
                        .OrderByDescending(c => c.WeightKg).FirstOrDefault();
                    sourceLotByBag[bag.Id] = main?.LotId ?? bag.LotId;
                    weightByBag[bag.Id] = bag.WeightKg;
                }
            }

            foreach (var b in dto.Bags)
            {
                rows.Add(new StockTransferBag
                {
                    StockTransferItemId = entities[i].Id,
                    BagId = b.BagId,
                    SourceLotId = sourceLotByBag.TryGetValue(b.BagId, out var lotId) ? lotId : null,
                    WeightKg = weightByBag.TryGetValue(b.BagId, out var w) ? w : 0m,
                    MoisturePercent = b.MoisturePercent,
                    ImpurityPercent = b.ImpurityPercent,
                    MoldLevel = b.MoldLevel,
                    PestLevel = b.PestLevel,
                    PackagingStatus = b.PackagingStatus,
                    QualityResult = b.QualityResult,
                    QualityNote = b.QualityNote,
                    Disposition = b.Disposition ?? StockTransferBagDispositions.Transfer,
                    QuarantineLocationId = b.QuarantineLocationId,
                    Note = b.Note,
                    CreatedBy = userId,
                    CreatedDate = now
                });
            }
        }

        if (rows.Count > 0)
        {
            await _transferBagRepository.CreateListAsync(rows);
            await _transferRepository.SaveChangesAsync();
        }
    }

    /// <summary>Xóa mềm bao của các dòng khi cập nhật lại phiếu Nháp.</summary>
    private async Task SoftDeleteItemBagsAsync(IEnumerable<int> itemIds, int? userId, DateTime now)
    {
        if (_transferBagRepository == null) return;
        var ids = itemIds.ToList();
        if (ids.Count == 0) return;

        var existing = await _transferBagRepository
            .FindByCondition(x => ids.Contains(x.StockTransferItemId) && !x.IsDeleted, false)
            .ToListAsync();
        foreach (var row in existing)
        {
            row.IsDeleted = true;
            row.UpdatedBy = userId;
            row.LastModifiedDate = now;
            await _transferBagRepository.UpdateAsync(row);
        }
        if (existing.Count > 0)
            await _transferRepository.SaveChangesAsync();
    }

    /// <summary>
    /// Một ô có DÙNG ĐƯỢC để cất hàng không — khớp CHÍNH XÁC bộ điều kiện của
    /// ValidateDestinationLocationAsync để gợi ý không bao giờ chọn ô sẽ bị từ chối khi nhận:
    /// đúng kho, đúng loại khu (thường/cách ly), không phải khu chờ xuất, không bị khóa xuất,
    /// đủ sức chứa, không phải cột một-loại đang chứa SKU khác, và khớp danh mục cho phép.
    /// </summary>
    private static bool IsLocationUsableForStorage(
        Location loc, int warehouseId, int productVariantId, decimal weightKg,
        int? productCategoryId, bool wantQuarantine)
    {
        if (loc == null) return false;
        if (!loc.IsActive || loc.IsDeleted) return false;
        if (loc.WarehouseId != warehouseId) return false;
        if (loc.IsQuarantine != wantQuarantine) return false;
        if (loc.IsOutboundStaging) return false;
        if (loc.OutboundLockOrderId.HasValue) return false;
        if (loc.MaxCapacity.HasValue && loc.CurrentOccupancy + weightKg > loc.MaxCapacity.Value) return false;
        // Cột một-loại & ràng buộc danh mục CHỈ áp cho cột thường (khu nhận hàng). Ô cách ly được
        // phép chứa nhiều SKU lỗi khác nhau nên bỏ qua hai điều kiện này khi wantQuarantine.
        if (!wantQuarantine)
        {
            if (loc.IsSingleTypeColumn && loc.CurrentOccupancy > 0 &&
                loc.CurrentProductVariantId.HasValue && loc.CurrentProductVariantId.Value != productVariantId) return false;
            if (loc.AllowedCategoryId.HasValue && loc.AllowedCategoryId.Value != productCategoryId) return false;
        }
        return true;
    }

    private async Task<int?> GetProductCategoryIdAsync(int productVariantId)
    {
        var variant = await _productVariantRepository.GetByIdAsync(productVariantId, x => x.Product);
        return variant?.Product?.ProductCategoryId;
    }

    /// <summary>Cột đang chứa BAO LẺ (bao chưa đầy) — không được xếp thêm bao vào để tránh loạn khi đổ đầy.</summary>
    private async Task<bool> LocationHasOpenBagAsync(int locationId)
    {
        if (_bagRepository == null) return false;
        return await _bagRepository.AnyAsync(x =>
            x.LocationId == locationId && x.Status == PaddyLotBagStatuses.Stored &&
            !x.IsFull && x.WeightKg > 0 && !x.IsDeleted);
    }

    /// <summary>Tập LocationId (trong danh sách candidateIds) đang có bao lẻ — dùng để loại khỏi gợi ý.</summary>
    private async Task<HashSet<int>> GetLocationIdsWithOpenBagAsync(List<int> candidateIds)
    {
        if (_bagRepository == null || candidateIds.Count == 0) return new HashSet<int>();
        var ids = await _bagRepository.FindByCondition(x =>
                x.LocationId != null && candidateIds.Contains(x.LocationId.Value) &&
                x.Status == PaddyLotBagStatuses.Stored && !x.IsFull && x.WeightKg > 0 && !x.IsDeleted)
            .Select(x => x.LocationId!.Value)
            .Distinct()
            .ToListAsync();
        return ids.ToHashSet();
    }

    /// <summary>
    /// Gợi ý ô LƯU (giống cách kiểm kê gợi ý vị trí): CHỈ trả về ô thật sự dùng được (đủ chỗ + cột
    /// một-loại + danh mục) VÀ — với cột thường — KHÔNG chứa bao lẻ (mỗi cột tối đa 1 bao lẻ), ưu tiên
    /// ô đang chứa cùng loại hàng → ô trống hẳn → ô còn nhiều chỗ → độ ưu tiên.
    /// </summary>
    private async Task<List<Location>> BuildStorageSuggestionsAsync(
        int warehouseId, int productVariantId, decimal weightKg, bool wantQuarantine)
    {
        var categoryId = await GetProductCategoryIdAsync(productVariantId);
        var candidates = (await _locationRepository.FindByCondition(x =>
                x.WarehouseId == warehouseId && x.IsActive && !x.IsDeleted &&
                x.IsQuarantine == wantQuarantine && !x.IsOutboundStaging && x.OutboundLockOrderId == null)
            .ToListAsync())
            .Where(x => IsLocationUsableForStorage(x, warehouseId, productVariantId, weightKg, categoryId, wantQuarantine))
            .ToList();

        // Cột thường: loại các cột đang có bao lẻ để không tạo 2 bao lẻ cùng cột.
        if (!wantQuarantine && candidates.Count > 0)
        {
            var openBagLocationIds = await GetLocationIdsWithOpenBagAsync(candidates.Select(x => x.Id).ToList());
            candidates = candidates.Where(x => !openBagLocationIds.Contains(x.Id)).ToList();
        }

        return candidates
            .OrderByDescending(x => x.CurrentProductVariantId == productVariantId)
            .ThenByDescending(x => x.CurrentOccupancy <= 0m)
            .ThenByDescending(x => x.MaxCapacity.HasValue ? x.MaxCapacity.Value - x.CurrentOccupancy : decimal.MaxValue / 2)
            .ThenBy(x => x.Priority)
            .ToList();
    }

    /// <summary>Vị trí đích mặc định khi người dùng chưa chọn (backend tự chọn ô tốt nhất còn dùng được).</summary>
    private async Task<int?> ResolveDestinationLocationAsync(int toWarehouseId, int productVariantId, decimal weightKg)
        => (await BuildStorageSuggestionsAsync(toWarehouseId, productVariantId, weightKg, wantQuarantine: false))
            .FirstOrDefault()?.Id;

    /// <summary>
    /// Đảm bảo vị trí đích DÙNG ĐƯỢC lúc nhận hàng: giữ ô người dùng đã chọn nếu còn hợp lệ,
    /// nếu không (đã bị chiếm bởi SKU khác / đầy / sai danh mục) thì tự chọn lại ô hợp lệ khác —
    /// nhờ vậy không còn văng lỗi "Vị trí đích đang chứa một loại sản phẩm khác".
    /// </summary>
    private async Task<int?> EnsureUsableDestinationAsync(
        int? chosenLocationId, int toWarehouseId, int productVariantId, decimal weightKg)
    {
        if (chosenLocationId.HasValue)
        {
            var chosen = await _locationRepository.GetByIdAsync(chosenLocationId.Value);
            var categoryId = await GetProductCategoryIdAsync(productVariantId);
            if (chosen != null &&
                IsLocationUsableForStorage(chosen, toWarehouseId, productVariantId, weightKg, categoryId, wantQuarantine: false) &&
                !await LocationHasOpenBagAsync(chosenLocationId.Value))
                return chosenLocationId;
        }
        return await ResolveDestinationLocationAsync(toWarehouseId, productVariantId, weightKg);
    }

    /// <summary>
    /// Chọn ô cách ly ở kho nguồn cho bao không đạt chất lượng. Ưu tiên ô người dùng đã chọn nếu
    /// còn hợp lệ; nếu không thì tự chọn ô cách ly tốt nhất còn dùng được (không văng lỗi giữa chừng).
    /// </summary>
    private async Task<Location> ResolveQuarantineLocationAsync(
        int sourceWarehouseId, int productVariantId, decimal weightKg, int? preferredLocationId)
    {
        if (preferredLocationId.HasValue)
        {
            var chosen = await _locationRepository.GetByIdAsync(preferredLocationId.Value);
            var categoryId = await GetProductCategoryIdAsync(productVariantId);
            if (chosen != null &&
                IsLocationUsableForStorage(chosen, sourceWarehouseId, productVariantId, weightKg, categoryId, wantQuarantine: true))
                return chosen;
            // Ô người dùng chọn không còn hợp lệ → tự chọn ô cách ly khác thay vì báo lỗi.
        }

        var best = (await BuildStorageSuggestionsAsync(sourceWarehouseId, productVariantId, weightKg, wantQuarantine: true))
            .FirstOrDefault();
        return best ?? throw new InvalidOperationException(
            "Không có ô cách ly khả dụng ở kho nguồn để đưa bao không đạt chất lượng vào. Vui lòng tạo/giải phóng ô cách ly.");
    }

    private static List<LocationSuggestionDto> ToLocationSuggestions(IEnumerable<Location> locations)
        => locations.Select(loc => new LocationSuggestionDto
        {
            LocationId = loc.Id,
            LocationName = FormatLocation(loc) ?? loc.SlotCode ?? $"Ô #{loc.Id}"
        }).ToList();

    /// <summary>Danh sách gợi ý ô lưu ở kho đích cho picker (chỉ tên vị trí, không chấm điểm/lý do).</summary>
    public async Task<ApiResponse> GetDestinationSuggestionsAsync(int toWarehouseId, int productVariantId, decimal weightKg)
    {
        var suggestions = await BuildStorageSuggestionsAsync(toWarehouseId, productVariantId, weightKg, wantQuarantine: false);
        return ApiResponse.Success(ToLocationSuggestions(suggestions));
    }

    /// <summary>Danh sách gợi ý ô CÁCH LY ở kho nguồn cho picker (chỉ tên vị trí).</summary>
    public async Task<ApiResponse> GetQuarantineSuggestionsAsync(int fromWarehouseId, int productVariantId, decimal weightKg)
    {
        var suggestions = await BuildStorageSuggestionsAsync(fromWarehouseId, productVariantId, weightKg, wantQuarantine: true);
        return ApiResponse.Success(ToLocationSuggestions(suggestions));
    }

    /// <summary>Danh sách bao ở ĐỈNH cột nguồn có thể chọn để chuyển kho (picker theo BAO).</summary>
    public async Task<ApiResponse> GetSourceBagsAsync(int fromWarehouseId, int fromLocationId, int? productVariantId)
    {
        if (_bagRepository == null)
            return ApiResponse.UnprocessableEntity("Dịch vụ quản lý bao chưa được cấu hình.");

        var query = _bagRepository.FindByCondition(x =>
                x.LocationId == fromLocationId && x.Status == PaddyLotBagStatuses.Stored &&
                x.WeightKg > 0 && !x.IsDeleted)
            .Include(x => x.Lot).ThenInclude(l => l.ProductVariant);

        var bags = await query
            .OrderByDescending(x => x.StackOrder).ThenByDescending(x => x.Id)
            .ToListAsync();

        var result = bags
            .Where(x => !productVariantId.HasValue || x.Lot.ProductVariantId == productVariantId.Value)
            .Select(x => new SourceColumnBagDto
            {
                BagId = x.Id,
                BagNo = x.BagNo,
                QrCode = x.QrCode,
                StackOrder = x.StackOrder,
                WeightKg = x.WeightKg,
                IsFull = x.IsFull,
                LotId = x.LotId,
                LotCode = x.Lot?.LotCode,
                ProductVariantId = x.Lot?.ProductVariantId ?? 0,
                ProductVariantName = x.Lot?.ProductVariant?.Name,
                LotQualityStatus = x.Lot?.QualityStatus
            })
            .ToList();

        return ApiResponse.Success(result);
    }

    /// <summary>
    /// Xuất từng bao của một dòng khỏi cột nguồn theo cách xử lý đã chốt:
    /// TRANSFER → đưa vào trạng thái Đang chuyển; QUARANTINE → chuyển vào ô cách ly kho nguồn;
    /// DISPOSE → bỏ nguyên bao. Mọi trường hợp đều trừ tồn cột nguồn + ghi lịch sử bag movement.
    /// </summary>
    private async Task DispatchItemBagsAsync(
        StockTransfer transfer,
        StockTransferItem item,
        List<StockTransferBag> bagRows,
        int userId,
        DateTime now)
    {
        if (_bagRepository == null || _transferBagRepository == null)
            throw new InvalidOperationException("Dịch vụ quản lý bao chưa được cấu hình.");

        var bagIds = bagRows.Select(r => r.BagId).ToList();
        var bags = await _bagRepository.FindByCondition(x => bagIds.Contains(x.Id) && !x.IsDeleted, false)
            .Include(x => x.Contents).ThenInclude(c => c.Lot).ToListAsync();
        var bagsById = bags.ToDictionary(x => x.Id);
        var quarantineStackOrder = new Dictionary<int, int>();

        foreach (var row in bagRows)
        {
            if (!bagsById.TryGetValue(row.BagId, out var bag))
                throw new InvalidOperationException($"Không tìm thấy bao ID {row.BagId} để xuất chuyển.");

            var fromLocationId = bag.LocationId;
            var contentGroups = bag.Contents.Where(c => !c.IsDeleted && c.WeightKg > 0)
                .GroupBy(c => c.LotId).ToList();
            var representativeLotId = contentGroups
                .OrderByDescending(g => g.Sum(c => c.WeightKg)).FirstOrDefault()?.Key ?? bag.LotId;

            row.WeightKg = bag.WeightKg;
            row.SourceLotId ??= representativeLotId;

            // Trừ tồn cột nguồn theo từng thành phần lô (mọi cách xử lý đều rời cột nguồn).
            foreach (var g in contentGroups)
            {
                var sourceLot = g.First().Lot;
                await MoveInventoryAsync(
                    item.ProductVariantId, transfer.FromWarehouseId, fromLocationId,
                    g.Sum(c => c.WeightKg), isExport: true, sourceLot.CostPricePerKg,
                    transfer.Id, item.Id, userId, now,
                    $"Xuất bao #{bag.BagNo} (lô {sourceLot.LotCode}) khỏi cột nguồn — {row.Disposition}",
                    sourceLot.Id);
            }

            switch (row.Disposition)
            {
                case StockTransferBagDispositions.Quarantine:
                {
                    var qLoc = await ResolveQuarantineLocationAsync(
                        transfer.FromWarehouseId, item.ProductVariantId, bag.WeightKg, row.QuarantineLocationId);
                    if (!quarantineStackOrder.TryGetValue(qLoc.Id, out var nextOrder))
                        nextOrder = (await _bagRepository.FindByCondition(x =>
                            x.LocationId == qLoc.Id && x.Status == PaddyLotBagStatuses.Stored && !x.IsDeleted)
                            .MaxAsync(x => (int?)x.StackOrder) ?? 0) + 1;

                    // Nhập bao vào ô cách ly (cùng kho nguồn) — cộng tồn ô cách ly.
                    foreach (var g in contentGroups)
                    {
                        var sourceLot = g.First().Lot;
                        await MoveInventoryAsync(
                            item.ProductVariantId, transfer.FromWarehouseId, qLoc.Id,
                            g.Sum(c => c.WeightKg), isExport: false, sourceLot.CostPricePerKg,
                            transfer.Id, item.Id, userId, now,
                            $"Cách ly bao #{bag.BagNo} (lô {sourceLot.LotCode}) tại ô {qLoc.SlotCode}",
                            sourceLot.Id);
                    }

                    bag.LocationId = qLoc.Id;
                    bag.Status = PaddyLotBagStatuses.Stored;
                    bag.BagKind = PaddyLotBagKinds.Quarantine;
                    bag.StackOrder = nextOrder;
                    bag.OpenBagKey = null;
                    bag.UpdatedBy = userId;
                    bag.LastModifiedDate = now;
                    await _bagRepository.UpdateAsync(bag);
                    quarantineStackOrder[qLoc.Id] = nextOrder + 1;
                    row.QuarantineLocationId = qLoc.Id;
                    await RecordBagMovementAsync(bag, PaddyLotBagMovementTypes.TransferQuarantine,
                        fromLocationId, qLoc.Id, transfer.Id, item.Id, userId, now);
                    break;
                }
                case StockTransferBagDispositions.Dispose:
                {
                    // Bỏ nguyên bao — giảm phần còn lại của lô nguồn tương ứng (hàng rời khỏi kho).
                    foreach (var g in contentGroups)
                    {
                        var sourceLot = await _paddyLotRepository.GetByIdAsync(g.Key);
                        if (sourceLot != null)
                        {
                            sourceLot.RemainingWeightKg = Math.Max(0m, sourceLot.RemainingWeightKg - g.Sum(c => c.WeightKg));
                            sourceLot.UpdatedBy = userId;
                            sourceLot.LastModifiedDate = now;
                            await _paddyLotRepository.UpdateAsync(sourceLot);
                        }
                    }
                    bag.Status = PaddyLotBagStatuses.Disposed;
                    bag.LocationId = null;
                    bag.StackOrder = 0;
                    bag.OpenBagKey = null;
                    bag.UpdatedBy = userId;
                    bag.LastModifiedDate = now;
                    await _bagRepository.UpdateAsync(bag);
                    await RecordBagMovementAsync(bag, PaddyLotBagMovementTypes.TransferDispose,
                        fromLocationId, null, transfer.Id, item.Id, userId, now);
                    break;
                }
                default: // TRANSFER — bao đạt chất lượng, đưa vào trạng thái Đang chuyển.
                {
                    bag.Status = PaddyLotBagStatuses.InTransit;
                    bag.LocationId = null;
                    bag.StackOrder = 0;
                    bag.OpenBagKey = null;
                    bag.UpdatedBy = userId;
                    bag.LastModifiedDate = now;
                    await _bagRepository.UpdateAsync(bag);
                    await RecordBagMovementAsync(bag, PaddyLotBagMovementTypes.TransferDispatch,
                        fromLocationId, null, transfer.Id, item.Id, userId, now);
                    break;
                }
            }

            await _transferBagRepository.UpdateAsync(row);
        }
    }

    private readonly record struct DestinationInboundLine(int ProductVariantId, int? LotId, decimal WeightKg, decimal CostPrice);

    /// <summary>
    /// Nhận các bao ĐẠT ở kho đích: sinh lô mới gắn kho đích (copy chất lượng từ lô nguồn),
    /// cộng tồn đích, đặt bao vào cột đích, và bổ sung dòng cho phiếu nhập kho truy vết.
    /// </summary>
    private async Task ReceiveItemBagsAsync(
        StockTransfer transfer,
        StockTransferItem item,
        List<StockTransferBag> transferBagRows,
        int userId,
        DateTime now,
        List<DestinationInboundLine> inboundLines)
    {
        if (_bagRepository == null || _transferBagRepository == null)
            throw new InvalidOperationException("Dịch vụ quản lý bao chưa được cấu hình.");

        var bagIds = transferBagRows.Select(r => r.BagId).ToList();
        var bags = await _bagRepository.FindByCondition(x => bagIds.Contains(x.Id) && !x.IsDeleted, false)
            .Include(x => x.Contents).ThenInclude(c => c.Lot).ToListAsync();
        var bagsById = bags.ToDictionary(x => x.Id);
        var targetLotMap = new Dictionary<int, int>();

        // Sinh lô mới ở kho đích theo từng lô nguồn + cộng tồn đích.
        foreach (var contentGroup in bags.SelectMany(x => x.Contents)
                     .Where(c => !c.IsDeleted && c.WeightKg > 0)
                     .GroupBy(c => c.LotId))
        {
            var sourceLot = contentGroup.First().Lot;
            var contentWeight = contentGroup.Sum(c => c.WeightKg);
            var mappedLotId = await MoveLotToDestinationAsync(
                sourceLot, contentWeight, transfer.ToWarehouseId, item.ToLocationId, userId, now);
            targetLotMap[sourceLot.Id] = mappedLotId;
            await MoveInventoryAsync(
                item.ProductVariantId, transfer.ToWarehouseId, item.ToLocationId,
                contentWeight, isExport: false, sourceLot.CostPricePerKg,
                transfer.Id, item.Id, userId, now,
                $"Nhận thành phần lô {sourceLot.LotCode} chuyển từ kho {transfer.FromWarehouseId}",
                mappedLotId);
            inboundLines.Add(new DestinationInboundLine(item.ProductVariantId, mappedLotId, contentWeight, sourceLot.CostPricePerKg));
        }

        // Đặt bao vào cột đích và gắn lô đích cho từng bao.
        var nextStackOrder = item.ToLocationId.HasValue
            ? (await _bagRepository.FindByCondition(x => x.LocationId == item.ToLocationId && x.Status == PaddyLotBagStatuses.Stored && !x.IsDeleted)
                .MaxAsync(x => (int?)x.StackOrder) ?? 0) + 1
            : 0;

        foreach (var row in transferBagRows)
        {
            if (!bagsById.TryGetValue(row.BagId, out var bag)) continue;
            var oldLotId = bag.LotId;
            if (_bagContentRepository != null)
            {
                foreach (var content in bag.Contents.Where(c => !c.IsDeleted && c.WeightKg > 0))
                {
                    if (targetLotMap.TryGetValue(content.LotId, out var mapped))
                    {
                        content.LotId = mapped;
                        await _bagContentRepository.UpdateAsync(content);
                    }
                }
            }
            if (targetLotMap.TryGetValue(oldLotId, out var mappedRep))
                bag.LotId = mappedRep;
            else
                RefreshRepresentativeLot(bag);

            bag.LocationId = item.ToLocationId;
            bag.Status = PaddyLotBagStatuses.Stored;
            // Bao mở là detached theo từng location; full bag mới tham gia stack LIFO.
            var isDetachedOpenBag = !bag.IsFull && bag.BagKind == PaddyLotBagKinds.Finished && item.ToLocationId.HasValue;
            bag.StackOrder = isDetachedOpenBag ? 0 : nextStackOrder++;
            bag.OpenBagKey = isDetachedOpenBag
                ? $"{bag.Lot.ProductVariantId}:{transfer.ToWarehouseId}:{item.ToLocationId!.Value}"
                : null;
            bag.UpdatedBy = userId;
            bag.LastModifiedDate = now;
            await _bagRepository.UpdateAsync(bag);

            row.TargetLotId = bag.LotId;
            await _transferBagRepository.UpdateAsync(row);

            await RecordBagMovementAsync(bag, PaddyLotBagMovementTypes.TransferReceive,
                null, item.ToLocationId, transfer.Id, item.Id, userId, now);
        }
    }

    /// <summary>
    /// Tạo phiếu nhập kho tự động ở kho đích để truy vết nguồn gốc (xuất từ kho nào).
    /// SourceType = STOCK_TRANSFER, gắn StockTransferId; mỗi dòng gắn lô mới sinh.
    /// </summary>
    private async Task CreateDestinationInboundOrderAsync(
        StockTransfer transfer,
        List<DestinationInboundLine> inboundLines,
        int userId,
        DateTime now)
    {
        if (_inboundOrderRepository == null || _inboundStatusRepository == null || inboundLines.Count == 0)
            return;

        var confirmedStatus = await _inboundStatusRepository.FirstOrDefaultAsync(
            x => x.Code == InboundOrderStatusNames.Confirmed && !x.IsDeleted);
        if (confirmedStatus == null)
            throw new InvalidOperationException("Không tìm thấy trạng thái phiếu nhập 'Đã nhận hàng'.");

        var fromWarehouseName = transfer.FromWarehouse?.Name ?? transfer.FromWarehouseId.ToString();
        var order = new InboundOrder
        {
            WarehouseId = transfer.ToWarehouseId,
            InboundOrderStatusId = confirmedStatus.Id,
            SourceType = InboundOrderSourceTypeConstants.StockTransfer,
            StockTransferId = transfer.Id,
            POCode = transfer.TransferCode,
            OrganizationId = transfer.OrganizationId,
            Note = $"Nhập kho tự động từ chuyển kho nội bộ {transfer.TransferCode} (xuất từ kho {fromWarehouseName}).",
            TotalAssetValue = inboundLines.Sum(l => l.WeightKg * l.CostPrice),
            CompletedDate = now,
            CreatedBy = userId,
            CreatedDate = now
        };
        await _inboundOrderRepository.CreateAsync(order);
        await _inboundOrderRepository.SaveChangesAsync();

        if (_inboundOrderItemRepository != null)
        {
            var lines = inboundLines.Select(l => new InboundOrderItem
            {
                InboundOrderId = order.Id,
                ProductVariantId = l.ProductVariantId,
                PaddyLotId = l.LotId,
                QuantityOrdered = l.WeightKg,
                QuantityReceived = l.WeightKg,
                UnitCostPrice = l.CostPrice,
                ExpectedWeightKg = l.WeightKg,
                ActualWeightKg = l.WeightKg,
                QRScanned = false,
                CreatedBy = userId,
                CreatedDate = now
            }).ToList();
            await _inboundOrderItemRepository.CreateListAsync(lines);
            await _inboundOrderRepository.SaveChangesAsync();
        }
    }

    private async Task ValidateDestinationLocationAsync(
        int? locationId,
        int warehouseId,
        int productVariantId,
        decimal weightKg)
    {
        if (!locationId.HasValue) return;

        var location = await _locationRepository.GetByIdAsync(locationId.Value)
            ?? throw new InvalidOperationException($"Không tìm thấy vị trí đích ID {locationId.Value}.");
        if (!location.IsActive)
            throw new InvalidOperationException("Vị trí đích đã ngưng hoạt động.");
        if (location.WarehouseId != warehouseId)
            throw new InvalidOperationException("Vị trí đích không thuộc kho đích.");
        if (location.IsOutboundStaging)
            throw new InvalidOperationException("Không thể nhận hàng chuyển kho vào khu chờ xuất.");
        if (location.OutboundLockOrderId.HasValue)
            throw new InvalidOperationException($"Vị trí đích đang được phiếu xuất #{location.OutboundLockOrderId} khóa.");
        if (location.IsQuarantine)
            throw new InvalidOperationException("Không thể nhận hàng vào vị trí cách ly.");
        if (location.MaxCapacity.HasValue &&
            location.CurrentOccupancy + weightKg > location.MaxCapacity.Value)
            throw new InvalidOperationException(
                $"Vị trí đích không đủ sức chứa. Còn trống {Math.Max(0m, location.MaxCapacity.Value - location.CurrentOccupancy)} kg.");
        if (location.IsSingleTypeColumn &&
            location.CurrentOccupancy > 0 &&
            location.CurrentProductVariantId.HasValue &&
            location.CurrentProductVariantId.Value != productVariantId)
            throw new InvalidOperationException("Vị trí đích đang chứa một loại sản phẩm khác.");

        if (location.AllowedCategoryId.HasValue)
        {
            var productVariant = await _productVariantRepository.GetByIdAsync(
                productVariantId,
                x => x.Product);
            if (productVariant?.Product == null ||
                productVariant.Product.ProductCategoryId != location.AllowedCategoryId.Value)
                throw new InvalidOperationException(
                    "Sản phẩm không thuộc nhóm danh mục được phép cất giữ tại vị trí đích.");
        }
    }

    private async Task<int> MoveLotToDestinationAsync(
        PaddyLot lot,
        decimal weightKg,
        int toWarehouseId,
        int? toLocationId,
        int userId,
        DateTime now)
    {
        if (weightKg < lot.RemainingWeightKg)
        {
            var newLot = new PaddyLot
            {
                OrganizationId = lot.OrganizationId,
                LotCode = await GenerateSplitLotCodeAsync(lot.LotType, now),
                LotType = lot.LotType,
                ProductVariantId = lot.ProductVariantId,
                RiceVarietyId = lot.RiceVarietyId,
                StatusId = lot.StatusId,
                SourceReceiptId = null,
                SourceMillingOrderId = lot.SourceMillingOrderId,
                ParentLotId = lot.Id,
                WarehouseId = toWarehouseId,
                LocationId = toLocationId,
                InboundDate = lot.InboundDate,
                InitialWeightKg = weightKg,
                RemainingWeightKg = weightKg,
                CostPricePerKg = lot.CostPricePerKg,
                QualityStatus = lot.QualityStatus,
                QrCode = "PL-" + Guid.NewGuid().ToString("N").ToUpper(),
                CreatedBy = userId,
                CreatedDate = now
            };
            await _paddyLotRepository.CreateAsync(newLot);
            await _paddyLotRepository.SaveChangesAsync();

            lot.RemainingWeightKg -= weightKg;
            lot.UpdatedBy = userId;
            lot.LastModifiedDate = now;
            await _paddyLotRepository.UpdateAsync(lot);
            await _paddyLotRepository.SaveChangesAsync();
            return newLot.Id;
        }

        lot.WarehouseId = toWarehouseId;
        lot.LocationId = toLocationId;
        lot.UpdatedBy = userId;
        lot.LastModifiedDate = now;
        await _paddyLotRepository.UpdateAsync(lot);
        await _paddyLotRepository.SaveChangesAsync();
        return lot.Id;
    }

    private async Task MoveInventoryAsync(
        int productVariantId,
        int warehouseId,
        int? locationId,
        decimal qty,
        bool isExport,
        decimal costPrice,
        int refId,
        int refItemId,
        int userId,
        DateTime now,
        string note,
        int? paddyLotId = null)
    {
        if (qty <= 0)
            throw new InvalidOperationException("Khối lượng chuyển phải lớn hơn 0.");

        var inventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
            productVariantId,
            warehouseId,
            locationId,
            paddyLotId);

        if (inventory == null)
        {
            if (isExport)
                throw new InvalidOperationException($"Không có tồn kho tại kho {warehouseId} để xuất {qty} kg.");

            inventory = new Inventory
            {
                WarehouseId = warehouseId,
                LocationId = locationId,
                ProductVariantId = productVariantId,
                PaddyLotId = paddyLotId,
                CostPrice = costPrice,
                QuantityOnHand = 0,
                QuantityReserved = 0,
                CreatedDate = now
            };
            await _inventoryRepository.CreateAsync(inventory);
            await _inventoryRepository.SaveChangesAsync();
        }

        var before = inventory.QuantityOnHand;







































        if (isExport)
        {
            var availableQty = inventory.QuantityOnHand - inventory.QuantityReserved;
            if (availableQty < qty)
                throw new InvalidOperationException(
                    $"Tồn kho khả dụng không đủ. Khả dụng {availableQty} kg, đang giữ {inventory.QuantityReserved} kg, cần xuất {qty} kg.");
            inventory.QuantityOnHand -= qty;
        }
        else
        {
            inventory.CostPrice = before > 0 && inventory.CostPrice > 0
                ? Math.Round(((before * inventory.CostPrice) + (qty * costPrice)) / (before + qty), 2)
                : costPrice;
            inventory.QuantityOnHand += qty;
        }

        inventory.LastModifiedDate = now;
        inventory.UpdatedBy = userId;
        await _inventoryRepository.UpdateAsync(inventory);

        await AdjustLocationOccupancyAsync(
            locationId,
            warehouseId,
            productVariantId,
            qty,
            isExport,
            userId,
            now);

        var tx = new InventoryTransaction
        {
            InventoryId = inventory.Id,
            WarehouseId = warehouseId,
            LocationId = locationId,
            ProductVariantId = productVariantId,
            PaddyLotId = paddyLotId,
            TransactionType = isExport
                ? InventoryTransactionTypeConstants.Export
                : InventoryTransactionTypeConstants.Import,
            ReferenceType = InventoryReferenceTypeConstants.StockTransfer,
            ReferenceId = refId,
            ReferenceItemId = refItemId,
            Quantity = isExport ? -qty : qty,
            BeforeQuantity = before,
            AfterQuantity = inventory.QuantityOnHand,
            WeightKg = qty,
            Note = note,
            CreatedDate = now,
            CreatedBy = userId
        };

        await _inventoryTransactionRepository.CreateWithColumnTotalsAsync(tx);
        await _inventoryTransactionRepository.SaveChangesAsync();
    }

    private async Task AdjustLocationOccupancyAsync(
        int? locationId,
        int warehouseId,
        int productVariantId,
        decimal quantity,
        bool isExport,
        int userId,
        DateTime now)
    {
        if (!locationId.HasValue) return;

        var location = await _locationRepository.GetByIdAsync(locationId.Value)
            ?? throw new InvalidOperationException($"Không tìm thấy vị trí ID {locationId.Value}.");
        if (location.WarehouseId != warehouseId)
            throw new InvalidOperationException("Vị trí tồn kho không thuộc kho đang xử lý.");

        if (isExport)
        {
            location.CurrentOccupancy = Math.Max(0m, location.CurrentOccupancy - quantity);
            if (location.CurrentOccupancy == 0)
                location.CurrentProductVariantId = null;
        }
        else
        {
            location.CurrentOccupancy += quantity;
            location.CurrentProductVariantId ??= productVariantId;
        }

        location.UpdatedBy = userId;
        location.LastModifiedDate = now;
        await _locationRepository.UpdateAsync(location);
    }

    private async Task<bool> IsLotBlockedForTransferAsync(int statusId)
    {
        var status = await _lotStatusRepository.GetByIdAsync(statusId);
        return status != null && status.Code == LotStatusCodeConstants.Quarantine;
    }

    private Task<StockTransferStatus?> GetStatusAsync(string code)
        => _statusRepository.FirstOrDefaultAsync(x => x.Code == code && !x.IsDeleted);

    private async Task<string> GenerateTransferCodeAsync(DateTime now)
    {
        var baseCode = $"ST-{now:yyyyMMdd}";
        var count = await _transferRepository
            .FindByCondition(x => x.TransferCode.StartsWith(baseCode))
            .CountAsync();

        for (var attempt = 1; attempt <= 20; attempt++)
        {
            var code = $"{baseCode}-{count + attempt:D4}";
            if (!await _transferRepository.AnyAsync(x => x.TransferCode == code))
                return code;
        }

        return $"{baseCode}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }

    private async Task<string> GenerateSplitLotCodeAsync(string lotType, DateTime now)
    {
        var baseCode = $"LOT-{lotType}-{now:yyyyMMdd}";
        var count = await _paddyLotRepository
            .FindByCondition(x => x.LotCode.StartsWith(baseCode))
            .CountAsync();

        for (var attempt = 1; attempt <= 20; attempt++)
        {
            var code = $"{baseCode}-{count + attempt:D4}";
            if (!await _paddyLotRepository.AnyAsync(x => x.LotCode == code))
                return code;
        }

        return $"{baseCode}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }

    private static bool IsStatus(StockTransfer transfer, string statusCode)
        => string.Equals(transfer.Status?.Code, statusCode, StringComparison.OrdinalIgnoreCase);

    private async Task RecordBagMovementAsync(PaddyLotBag bag, string movementType, int? fromLocationId,
        int? toLocationId, int referenceId, int referenceItemId, int userId, DateTime now)
    {
        if (_bagMovementRepository == null) return;
        await _bagMovementRepository.CreateAsync(new PaddyLotBagMovement
        {
            BagId = bag.Id,
            MovementType = movementType,
            FromLocationId = fromLocationId,
            ToLocationId = toLocationId,
            WeightKg = bag.WeightKg,
            BeforeWeightKg = bag.WeightKg,
            AfterWeightKg = bag.WeightKg,
            ReferenceType = InventoryReferenceTypeConstants.StockTransfer,
            ReferenceId = referenceId,
            ReferenceItemId = referenceItemId,
            CreatedBy = userId,
            CreatedDate = now
        });
    }

    private static void RefreshRepresentativeLot(PaddyLotBag bag)
    {
        var active = bag.Contents.Where(x => !x.IsDeleted && x.WeightKg > 0.0005m).ToList();
        if (active.Count == 0)
            throw new InvalidOperationException($"Bao #{bag.BagNo} không có thành phần lô hợp lệ.");
        if (active.Any(x => x.LotId == bag.LotId)) return;
        bag.LotId = active.OrderByDescending(x => x.WeightKg).ThenBy(x => x.Id).First().LotId;
    }

    private static StockTransferItem ToEntity(
        StockTransferItemDto item,
        int transferId,
        int? userId,
        DateTime now)
        => new()
        {
            StockTransferId = transferId,
            ProductVariantId = item.ProductVariantId,
            PaddyLotId = item.PaddyLotId,
            FromLocationId = item.FromLocationId,
            ToLocationId = item.ToLocationId,
            WeightKg = item.WeightKg,
            Note = item.Note?.Trim(),
            BagIdsJson = item.BagIds.Count == 0 ? null : JsonSerializer.Serialize(item.BagIds),
            CreatedBy = userId,
            CreatedDate = now
        };

    private static StockTransferItemDto ToItemDto(StockTransferItem item)
        => new()
        {
            ProductVariantId = item.ProductVariantId,
            PaddyLotId = item.PaddyLotId,
            FromLocationId = item.FromLocationId,
            ToLocationId = item.ToLocationId,
            WeightKg = item.WeightKg,
            Note = item.Note
            ,BagIds = string.IsNullOrWhiteSpace(item.BagIdsJson) ? new List<int>() : JsonSerializer.Deserialize<List<int>>(item.BagIdsJson) ?? new List<int>()
        };

    private static StockTransferDetailDto ToDto(StockTransfer x)
    {
        var items = x.StockTransferItems.Where(i => !i.IsDeleted).ToList();
        var canInferSourceFromLot = !IsStatus(x, StockTransferStatusNames.Completed);

        // Khối lượng dòng: nếu chưa lưu (phiếu cũ) thì suy ra từ tổng bao ĐẠT.
        static decimal EffectiveItemWeight(StockTransferItem i)
        {
            if (i.WeightKg > 0) return i.WeightKg;
            return i.Bags.Where(b => !b.IsDeleted && b.Disposition == StockTransferBagDispositions.Transfer)
                .Sum(b => b.WeightKg > 0 ? b.WeightKg : (b.Bag != null ? b.Bag.WeightKg : 0m));
        }

        return new StockTransferDetailDto
        {
            Id = x.Id,
            OrganizationId = x.OrganizationId,
            TransferCode = x.TransferCode,
            StatusId = x.StatusId,
            StatusName = x.Status?.Name,
            StatusCode = x.Status?.Code,
            StatusColor = x.Status?.Color,
            FromWarehouseId = x.FromWarehouseId,
            FromWarehouseName = x.FromWarehouse?.Name,
            ToWarehouseId = x.ToWarehouseId,
            ToWarehouseName = x.ToWarehouse?.Name,
            AssignedUserId = x.AssignedUserId,
            TransferDate = x.TransferDate,
            Note = x.Note,
            ItemCount = items.Count,
            TotalWeightKg = items.Sum(EffectiveItemWeight),
            Items = items.Select(i => new StockTransferItemDetailDto
            {
                Id = i.Id,
                ProductVariantId = i.ProductVariantId,
                SKU = i.ProductVariant?.SKU,
                ProductVariantName = i.ProductVariant?.Name,
                PaddyLotId = i.PaddyLotId,
                LotCode = i.PaddyLot?.LotCode,
                FromLocationId = i.FromLocationId ??
                    (canInferSourceFromLot ? i.PaddyLot?.LocationId : null),
                FromLocationName =
                    FormatLocation(i.FromLocation) ??
                    (canInferSourceFromLot ? FormatLocation(i.PaddyLot?.Location) : null) ??
                    "Tồn cấp kho nguồn",
                ToLocationId = i.ToLocationId,
                ToLocationName = FormatLocation(i.ToLocation),
                WeightKg = EffectiveItemWeight(i),
                Note = i.Note,
                BagIds = string.IsNullOrWhiteSpace(i.BagIdsJson)
                    ? new List<int>()
                    : JsonSerializer.Deserialize<List<int>>(i.BagIdsJson) ?? new List<int>(),
                Bags = i.Bags.Where(b => !b.IsDeleted).Select(b => new StockTransferBagDetailDto
                {
                    Id = b.Id,
                    BagId = b.BagId,
                    BagNo = b.Bag?.BagNo,
                    QrCode = b.Bag?.QrCode,
                    SourceLotId = b.SourceLotId,
                    SourceLotCode = b.SourceLot?.LotCode,
                    // Phiếu tạo trước khi lưu kg bao (=0) → hiển thị theo kg bao hiện tại.
                    WeightKg = b.WeightKg > 0 ? b.WeightKg : (b.Bag != null ? b.Bag.WeightKg : 0m),
                    MoisturePercent = b.MoisturePercent,
                    ImpurityPercent = b.ImpurityPercent,
                    MoldLevel = b.MoldLevel,
                    PestLevel = b.PestLevel,
                    PackagingStatus = b.PackagingStatus,
                    QualityResult = b.QualityResult,
                    QualityNote = b.QualityNote,
                    Disposition = b.Disposition,
                    QuarantineLocationId = b.QuarantineLocationId,
                    QuarantineLocationName = FormatLocation(b.QuarantineLocation),
                    TargetLotId = b.TargetLotId,
                    TargetLotCode = b.TargetLot?.LotCode,
                    Note = b.Note
                }).ToList()
            }).ToList(),
            CreatedDate = x.CreatedDate,
            LastModifiedDate = x.LastModifiedDate
        };
    }

    private static string? FormatLocation(Location? location)
    {
        if (location == null) return null;
        var parts = new[]
        {
            location.ZoneName,
            location.ShelfRow,
            location.ShelfLevel,
            location.SlotCode
        }.Where(x => !string.IsNullOrWhiteSpace(x));
        return string.Join("-", parts);
    }
}
