using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    private readonly IRepositoryBase<Location, int> _locationRepository;
    private readonly IRepositoryBase<ProductVariant, int> _productVariantRepository;
    private readonly IPaddyLotRepository _paddyLotRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryTransactionRepository _inventoryTransactionRepository;
    private readonly INotificationDispatcher _notificationDispatcher;

    public StockTransferService(
        IStockTransferRepository transferRepository,
        IRepositoryBase<StockTransferItem, int> itemRepository,
        IRepositoryBase<StockTransferStatus, int> statusRepository,
        IRepositoryBase<LotStatus, int> lotStatusRepository,
        IRepositoryBase<Warehouse, int> warehouseRepository,
        IRepositoryBase<Location, int> locationRepository,
        IRepositoryBase<ProductVariant, int> productVariantRepository,
        IPaddyLotRepository paddyLotRepository,
        IInventoryRepository inventoryRepository,
        IInventoryTransactionRepository inventoryTransactionRepository,
        INotificationDispatcher notificationDispatcher)
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

            var items = obj.Items.Select(item => ToEntity(item, transfer.Id, obj.CreatedBy, now)).ToList();
            await _itemRepository.CreateListAsync(items);
            await _transferRepository.SaveChangesAsync();
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
        var entities = await _transferRepository
            .FindByCondition(x => !x.IsDeleted, false)
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
        return ApiResponse.Success(ToDto(entity));
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
            x.Status.Name != StockTransferStatusNames.Cancelled);

        var inTransitCount = await query.CountAsync(x =>
            x.Status.Name == StockTransferStatusNames.InTransit);

        var totalTransferredWeightKg = await query
            .Where(x =>
                x.TransferDate >= monthStart &&
                x.TransferDate < nextMonth &&
                (x.Status.Name == StockTransferStatusNames.InTransit ||
                 x.Status.Name == StockTransferStatusNames.Completed))
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
            foreach (var currentItem in currentItems)
            {
                currentItem.IsDeleted = true;
                currentItem.UpdatedBy = obj.UpdatedBy;
                currentItem.LastModifiedDate = now;
            }
            if (currentItems.Count > 0)
                await _itemRepository.UpdateListAsync(currentItems);

            var replacementItems = obj.Items
                .Select(item => ToEntity(item, entity.Id, obj.UpdatedBy, now))
                .ToList();
            await _itemRepository.CreateListAsync(replacementItems);

            await _transferRepository.SaveChangesAsync();
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
                        item.ProductVariantId,
                        transfer.FromWarehouseId,
                        item.FromLocationId);
                    costPrice = sourceInventory?.CostPrice ?? 0m;
                }

                await MoveInventoryAsync(
                    item.ProductVariantId,
                    transfer.FromWarehouseId,
                    item.FromLocationId,
                    item.WeightKg,
                    isExport: true,
                    costPrice,
                    transfer.Id,
                    item.Id,
                    dispatchedById,
                    now,
                    $"Xuất chuyển từ kho {transfer.FromWarehouseId} đến kho {transfer.ToWarehouseId}",
                    item.PaddyLotId);
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
            foreach (var item in activeItems)
            {
                var duplicated = await _inventoryTransactionRepository.AnyAsync(x =>
                    x.ReferenceType == InventoryReferenceTypeConstants.StockTransfer &&
                    x.ReferenceId == transfer.Id &&
                    x.ReferenceItemId == item.Id &&
                    x.TransactionType == InventoryTransactionTypeConstants.Import);
                if (duplicated)
                    throw new InvalidOperationException($"Dòng hàng {item.Id} đã được nhận trước đó.");

                await ValidateDestinationLocationAsync(
                    item.ToLocationId,
                    transfer.ToWarehouseId,
                    item.ProductVariantId,
                    item.WeightKg);

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
                        lot,
                        item.WeightKg,
                        transfer.ToWarehouseId,
                        item.ToLocationId,
                        receivedById,
                        now);
                }
                else
                {
                    var sourceInventory = await _inventoryRepository.GetByVariantWarehouseLocationAsync(
                        item.ProductVariantId,
                        transfer.FromWarehouseId,
                        item.FromLocationId);
                    costPrice = sourceInventory?.CostPrice ?? 0m;
                }

                await MoveInventoryAsync(
                    item.ProductVariantId,
                    transfer.ToWarehouseId,
                    item.ToLocationId,
                    item.WeightKg,
                    isExport: false,
                    costPrice,
                    transfer.Id,
                    item.Id,
                    receivedById,
                    now,
                    $"Nhận hàng chuyển từ kho {transfer.FromWarehouseId}",
                    targetLotId);
            }

            transfer.StatusId = completedStatus.Id;
            transfer.UpdatedBy = receivedById;
            transfer.LastModifiedDate = now;
            await _transferRepository.UpdateAsync(transfer);
            await _transferRepository.SaveChangesAsync();
            await _transferRepository.EndTransactionAsync();

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
                .ThenInclude(i => i.ToLocation);
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
            if (item.WeightKg <= 0)
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
                if (destinationLocation.IsQuarantine)
                    return "Không thể chọn vị trí cách ly làm vị trí nhận hàng.";
            }

            if (item.PaddyLotId.HasValue)
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

            if (checkAvailableStock)
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

    private Task<StockTransferStatus?> GetStatusAsync(string name)
        => _statusRepository.FirstOrDefaultAsync(x => x.Name == name && !x.IsDeleted);

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

    private static bool IsStatus(StockTransfer transfer, string statusName)
        => string.Equals(transfer.Status?.Name, statusName, StringComparison.OrdinalIgnoreCase);

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
        };

    private static StockTransferDetailDto ToDto(StockTransfer x)
    {
        var items = x.StockTransferItems.Where(i => !i.IsDeleted).ToList();
        var canInferSourceFromLot = !IsStatus(x, StockTransferStatusNames.Completed);
        return new StockTransferDetailDto
        {
            Id = x.Id,
            OrganizationId = x.OrganizationId,
            TransferCode = x.TransferCode,
            StatusId = x.StatusId,
            StatusName = x.Status?.Name,
            StatusColor = x.Status?.Color,
            FromWarehouseId = x.FromWarehouseId,
            FromWarehouseName = x.FromWarehouse?.Name,
            ToWarehouseId = x.ToWarehouseId,
            ToWarehouseName = x.ToWarehouse?.Name,
            AssignedUserId = x.AssignedUserId,
            TransferDate = x.TransferDate,
            Note = x.Note,
            ItemCount = items.Count,
            TotalWeightKg = items.Sum(i => i.WeightKg),
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
                WeightKg = i.WeightKg,
                Note = i.Note
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
